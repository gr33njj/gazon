using System.IO;
using Gazon.Core;
using Gazon.Customers;
using Gazon.Player;
using Gazon.UI;
using Gazon.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gazon.EditorTools
{
    /// <summary>
    /// Собирает полную комнату 1:1 из gamedev/pvz_simulator_mvp.html (координаты см. Core/RoomLayout.cs)
    /// кодом через UnityEditor API. Запускать на ПУСТОЙ новой сцене (File → New Scene) — CI это делает
    /// сама (см. Assets/Editor/BuildScript.cs, RegenerateScene). Меню: Gazon → Собрать сцену вертикального среза.
    ///
    /// Сознательные упрощения относительно MVP (см. Docs/Decisions.md, порт 2026-07-20):
    /// не портированы декоративные мелочи (текстуры вывесок, шашечный пол, конус/куст, анимация
    /// подъезжающей газели и "полёта" коробок с неё) — вся функциональная геометрия (стены, проёмы,
    /// стойка, окна, стеллажи, примерочная, курилка, стол возвратов, точки входа/выхода) 1:1.
    /// Столкновения со стенами/стеллажами/стойкой берёт на себя CharacterController + BoxCollider —
    /// собственный AABB-движок коллизий из MVP не нужен.
    /// </summary>
    public static class SceneBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string MaterialFolder = "Assets/Materials";

        // Пути к сторонним ассетам из Assets/ThirdPatry (см. Docs/Decisions.md, визуальный
        // проход 2026-07-20). Не понравится результат — меняется одной строкой здесь.
        private const string BoxModelPath = "Assets/ThirdPatry/Box by Kenney - HvjissDrdr/box.obj";
        private const string ShelfModelPath = "Assets/ThirdPatry/Shelves by J-Toastie - OD78iJOQoN/WarehouseShelving.obj";
        private const string CustomerModelPath = "Assets/ThirdPatry/Universal Base Characters[Standard]/Universal Base Characters[Standard]/Base Characters/Unity/Superhero_Male_FullBody.fbx";

        // KayKit City Builder Bits — целиком .gltf, нужен пакет glTFast (Package Manager →
        // Unity Registry → "glTFast"), иначе LoadModel просто залогирует ошибку и пропустит пропы.
        private const string KayKitCityFolder = "Assets/ThirdPatry/KayKit_City_Builder_Bits_1.0_FREE/KayKit_City_Builder_Bits_1.0_FREE/Assets/gltf";

        private const int CellsPerShelf = RoomLayout.CellsPerRack;
        private const float ShelfLength = RoomLayout.RackLength;
        private const float ShelfDepth = RoomLayout.RackDepth;
        private const float ShelfCellHeight = 0.8f;
        private const float ShelfBottomY = 0.9f;

        [MenuItem("Gazon/Собрать сцену вертикального среза")]
        public static void BuildScene()
        {
            if (GameObject.Find("GameManager") != null)
            {
                Debug.LogError("Объект 'GameManager' уже есть на сцене. Запустите это на новой пустой " +
                                "сцене (File → New Scene), иначе всё задвоится.");
                return;
            }

            EnsureFolder(PrefabFolder);

            var boxPrefab = CreateBoxPrefab();
            var customerPrefab = CreateCustomerPrefab();
            var courierPrefab = CreateCourierPrefab();

            var player = BuildPlayer();
            BuildManagers();
            BuildLighting();
            BuildRoom();
            BuildShelves();
            var courierSpawner = BuildCourierSpawner(courierPrefab);
            BuildDock(boxPrefab, courierSpawner);
            BuildCounterAndWindows();
            BuildReturnsTable(boxPrefab);
            BuildFittingRoom();
            BuildSmokeDoor(player.GetComponent<PlayerBuffs>());
            BuildCustomerSpawner(customerPrefab);
            BuildCellHighlight();
            BuildStreetDressing();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Сцена собрана. Сохрани её (Ctrl+S) и нажми Play.");
        }

        // ---------- Игрок и менеджеры ----------

        private static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            // Y=0 (пол) — CharacterController по умолчанию center=(0,1,0)/height=2, значит его
            // нижняя точка окажется ровно на полу. Раньше здесь стоял y=1, из-за чего игрок
            // висел на метр над полом, а камера (y=1.6 локально) оказывалась на 2.6 — отсюда
            // и ощущение "я какой-то высокий" в новой полноразмерной комнате.
            player.transform.position = new Vector3(13f, 0f, 11.5f); // MVP: P.x=13,z=11.5

            player.AddComponent<CharacterController>();
            var interaction = player.AddComponent<PlayerInteraction>();
            var controller = player.AddComponent<PlayerController>();
            player.AddComponent<PlayerBuffs>();

            var camera = Camera.main;
            GameObject camGO;
            if (camera != null)
            {
                camGO = camera.gameObject;
                camGO.transform.SetParent(player.transform);
            }
            else
            {
                camGO = new GameObject("Main Camera");
                camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                camGO.tag = "MainCamera";
                camGO.transform.SetParent(player.transform);
            }
            camGO.transform.localPosition = new Vector3(0f, RoomLayout.EyeHeight, 0f); // MVP: EYE=1.7
            camGO.transform.localRotation = Quaternion.identity;

            var handAnchor = new GameObject("HandAnchor");
            handAnchor.transform.SetParent(camGO.transform);
            handAnchor.transform.localPosition = new Vector3(0.3f, -0.3f, 0.7f);

            SetPrivateField(controller, "playerCamera", camGO.GetComponent<Camera>());
            SetPrivateField(interaction, "playerCamera", camGO.GetComponent<Camera>());
            SetPrivateField(interaction, "handAnchor", handAnchor.transform);

            return player;
        }

        private static void BuildManagers()
        {
            var go = new GameObject("Managers");
            go.AddComponent<GameManager>();
            go.AddComponent<MinigameController>();
            go.AddComponent<NightInventoryController>();
            go.AddComponent<GameUI>();
        }

        // ---------- Освещение ----------

        /// <summary>BuildScene() стартует с EditorSceneManager.NewScene(EmptyScene) — а это
        /// значит НИ ОДНОГО источника света на сцене (ни направленного, ни ambient-настроек).
        /// Без этого весь URP-рендер держится на нулевой ambient-подсветке по умолчанию —
        /// отсюда и "цвета не прям красочные": материалы физически не освещены.</summary>
        private static void BuildLighting()
        {
            var sunGO = new GameObject("Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.58f);
        }

        // ---------- Комната ----------

        private static void BuildRoom()
        {
            var floorMat = CreateMaterial("Floor", HexColor(0xE9E3D5));
            var wallMat = CreateMaterial("Wall", HexColor(0xF0EEE7));
            var ceilingMat = CreateMaterial("Ceiling", HexColor(0xD8DCE4));
            var dockMat = CreateMaterial("DockFloor", HexColor(0xC8E2CF));
            var lampMat = CreateMaterial("Lamp", Color.white);

            float w = RoomLayout.RoomWidth, d = RoomLayout.RoomDepth, h = RoomLayout.RoomHeight;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(w / 2f, 0f, d / 2f);
            floor.transform.localScale = new Vector3(w / 10f, 1f, d / 10f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            CreatePanel(null, "DockFloor", dockMat,
                new Vector3(RoomLayout.DockX + RoomLayout.DockW / 2f, 0.01f, RoomLayout.DockZ + RoomLayout.DockD / 2f),
                new Vector3(RoomLayout.DockW, 0.02f, RoomLayout.DockD));

            // Задняя стена (Z=0) с проёмом под курилку (SMOKE_DOOR).
            CreatePanel(null, "Wall_Back_Left", wallMat,
                new Vector3((RoomLayout.SmokeDoorX - 0.5f) / 2f, h / 2f, -0.1f),
                new Vector3(RoomLayout.SmokeDoorX - 0.5f + 0.2f, h, 0.2f));
            CreatePanel(null, "Wall_Back_Right", wallMat,
                new Vector3((RoomLayout.SmokeDoorX + 0.5f + w) / 2f, h / 2f, -0.1f),
                new Vector3(w - (RoomLayout.SmokeDoorX + 0.5f), h, 0.2f));
            CreatePanel(null, "Wall_Back_Lintel", wallMat,
                new Vector3(RoomLayout.SmokeDoorX, h - 0.45f, -0.1f),
                new Vector3(1.0f, 0.9f, 0.2f));

            // Дальняя глухая стена (Z=d).
            CreatePanel(null, "Wall_Far", wallMat, new Vector3(w / 2f, h / 2f, d + 0.1f), new Vector3(w + 0.4f, h, 0.2f));

            // Левая стена (X=0) с проёмом под докстанцию (DOOR).
            CreatePanel(null, "Wall_Left_Lower", wallMat,
                new Vector3(-0.1f, h / 2f, RoomLayout.DoorZ0 / 2f), new Vector3(0.2f, h, RoomLayout.DoorZ0));
            CreatePanel(null, "Wall_Left_Upper", wallMat,
                new Vector3(-0.1f, h / 2f, (RoomLayout.DoorZ1 + d) / 2f), new Vector3(0.2f, h, d - RoomLayout.DoorZ1));
            CreatePanel(null, "Wall_Left_Lintel", wallMat,
                new Vector3(-0.1f, h - 0.4f, (RoomLayout.DoorZ0 + RoomLayout.DoorZ1) / 2f),
                new Vector3(0.2f, 0.8f, RoomLayout.DoorZ1 - RoomLayout.DoorZ0));

            // Правая стена (X=w) с проёмом под вход клиентов (ENTR).
            CreatePanel(null, "Wall_Right_Lower", wallMat,
                new Vector3(w + 0.1f, h / 2f, RoomLayout.EntranceZ0 / 2f), new Vector3(0.2f, h, RoomLayout.EntranceZ0));
            CreatePanel(null, "Wall_Right_Upper", wallMat,
                new Vector3(w + 0.1f, h / 2f, (RoomLayout.EntranceZ1 + d) / 2f), new Vector3(0.2f, h, d - RoomLayout.EntranceZ1));
            CreatePanel(null, "Wall_Right_Lintel", wallMat,
                new Vector3(w + 0.1f, h - 0.4f, (RoomLayout.EntranceZ0 + RoomLayout.EntranceZ1) / 2f),
                new Vector3(0.2f, 0.8f, RoomLayout.EntranceZ1 - RoomLayout.EntranceZ0));

            CreatePanel(null, "Ceiling", ceilingMat, new Vector3(w / 2f, h + 0.05f, d / 2f), new Vector3(w, 0.1f, d));

            for (float lx = 3f; lx < w; lx += 4f)
                for (float lz = 3f; lz < d; lz += 4f)
                    CreatePanel(null, "Lamp", lampMat, new Vector3(lx, h - 0.02f, lz), new Vector3(1.4f, 0.06f, 0.5f));
        }

        // ---------- Стеллажи (24 ячейки: А1-8, Б1-8, В1-8) ----------

        private static void BuildShelves()
        {
            var shelvesRoot = new GameObject("Shelves");

            foreach (var rack in RoomLayout.Racks)
            {
                var center = new Vector3(rack.X0 + ShelfLength / 2f, 0f, rack.Z + ShelfDepth / 2f);
                BuildShelfUnit(shelvesRoot.transform, rack.Label, center);
            }
        }

        private static void BuildShelfUnit(Transform parent, string letter, Vector3 position)
        {
            var shelfRoot = new GameObject($"Shelf_{letter}");
            shelfRoot.transform.SetParent(parent);
            shelfRoot.transform.position = position;

            var cellWidth = ShelfLength / CellsPerShelf;
            var halfLength = ShelfLength / 2f;
            var topY = ShelfBottomY + ShelfCellHeight;
            var midY = (ShelfBottomY + topY) / 2f;

            // Визуал — один готовый WarehouseShelving.obj вместо процедурных досок/стоек.
            // ShelfCell-объекты ниже остаются логикой поверх него без изменений.
            var visual = InstantiateModel(ShelfModelPath, shelfRoot.transform);
            if (visual != null)
            {
                FitToSizeStretch(visual, new Vector3(ShelfLength, topY, ShelfDepth));
                GroundAt(visual, 0f);
            }

            BuildShelfSign(shelfRoot.transform, letter, topY);

            for (int i = 0; i < CellsPerShelf; i++)
            {
                var cellX = -halfLength + cellWidth * (i + 0.5f);
                var go = new GameObject($"ShelfCell_{letter}{i + 1}");
                go.transform.SetParent(shelfRoot.transform);
                go.transform.localPosition = new Vector3(cellX, midY, 0f);

                var collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(cellWidth * 0.9f, ShelfCellHeight * 0.9f, ShelfDepth * 0.9f);

                var cell = go.AddComponent<ShelfCell>();
                SetPrivateField(cell, "label", $"{letter}{i + 1}");
            }
        }

        private static void BuildShelfSign(Transform parent, string letter, float topY)
        {
            // TextMesh (legacy) использует "GUI/Text Shader" — Built-in-only, под URP не
            // рендерится (отсюда "не подписанные ячейки"/невидимая табличка). TextMeshPro
            // рендерится корректно в обоих пайплайнах.
            var go = new GameObject($"Sign_{letter}");
            go.transform.SetParent(parent);
            go.transform.localPosition = new Vector3(0f, topY + 0.3f, ShelfDepth / 2f);

            var text = go.AddComponent<TMPro.TextMeshPro>();
            text.text = letter;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.fontSize = 24;
            text.enableAutoSizing = true;
            text.fontSizeMin = 4f;
            text.fontSizeMax = 72f;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1f, 0.5f);
        }

        // ---------- Докстанция + курьер ----------

        private static CourierSpawner BuildCourierSpawner(GameObject courierPrefab)
        {
            var go = new GameObject("CourierSpawner");
            var spawner = go.AddComponent<CourierSpawner>();

            var spawnPoint = new GameObject("CourierSpawnPoint");
            spawnPoint.transform.SetParent(go.transform);
            spawnPoint.transform.position = new Vector3(1.0f, 0f, (RoomLayout.DoorZ0 + RoomLayout.DoorZ1) / 2f);

            SetPrivateField(spawner, "courierPrefab", courierPrefab.GetComponent<Courier>());
            SetPrivateField(spawner, "spawnPoint", spawnPoint.transform);
            return spawner;
        }

        private static void BuildDock(GameObject boxPrefab, CourierSpawner courierSpawner)
        {
            var dock = new GameObject("Dock");
            dock.transform.position = new Vector3(RoomLayout.DockX, 0f, RoomLayout.DockZ);
            var spawner = dock.AddComponent<DockSpawner>();
            SetPrivateField(spawner, "boxPrefab", boxPrefab.GetComponent<Box>());
            SetPrivateField(spawner, "courierSpawner", courierSpawner);
        }

        // ---------- Стойка + 2 окна выдачи ----------

        private static void BuildCounterAndWindows()
        {
            var counterMat = CreateMaterial("Counter", HexColor(0x005BFF));
            var counterTrimMat = CreateMaterial("CounterTrim", HexColor(0x0040B8));
            var windowMat = CreateMaterial("WindowLedge", HexColor(0xFF3B8D));

            CreatePanel(null, "Counter", counterMat,
                new Vector3(RoomLayout.CounterX + RoomLayout.CounterW / 2f, 0.525f, RoomLayout.CounterZ + RoomLayout.CounterD / 2f),
                new Vector3(RoomLayout.CounterW, 1.05f, RoomLayout.CounterD));
            CreatePanel(null, "CounterTrim", counterTrimMat,
                new Vector3(RoomLayout.CounterX + RoomLayout.CounterW / 2f, 1.08f, RoomLayout.CounterZ + RoomLayout.CounterD / 2f),
                new Vector3(RoomLayout.CounterW + 0.2f, 0.06f, RoomLayout.CounterD + 0.2f));

            foreach (var wz in RoomLayout.WindowZ)
            {
                var go = new GameObject($"Window_{wz}");
                go.transform.position = new Vector3(RoomLayout.CounterX + RoomLayout.CounterW / 2f, 1.25f, wz);
                var collider = go.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.9f, 0.5f, 1.2f);
                var window = go.AddComponent<WindowStation>();

                CreatePanel(go.transform, "Ledge", windowMat,
                    new Vector3(0f, -0.13f, 0f), new Vector3(RoomLayout.CounterW + 0.24f, 0.02f, 1.2f));

                var stand = new GameObject("StandPoint");
                stand.transform.SetParent(go.transform);
                // MVP: c.tx=COUNTER.x+COUNTER.w+0.55 (мировая X); окно стоит в COUNTER.x+COUNTER.w/2,
                // отсюда локальный офсет = COUNTER.w/2+0.55. Y=-1.25, чтобы клиент стоял на полу (y=0).
                stand.transform.localPosition = new Vector3(RoomLayout.CounterW / 2f + 0.55f, -1.25f, 0f);

                SetPrivateField(window, "customerStandPoint", stand.transform);
            }
        }

        // ---------- Стол возвратов ----------

        private static void BuildReturnsTable(GameObject boxPrefab)
        {
            var mat = CreateMaterial("Returns", HexColor(0xFFB020));
            var go = CreatePanel(null, "ReturnsTable", mat,
                new Vector3(RoomLayout.ReturnsX + RoomLayout.ReturnsW / 2f, 0.45f, RoomLayout.ReturnsZ + RoomLayout.ReturnsD / 2f),
                new Vector3(RoomLayout.ReturnsW, 0.9f, RoomLayout.ReturnsD));
            go.AddComponent<ReturnsTable>();

            var spawnerGO = new GameObject("ReturnSpawner");
            var spawner = spawnerGO.AddComponent<ReturnSpawner>();
            SetPrivateField(spawner, "boxPrefab", boxPrefab.GetComponent<Box>());
        }

        // ---------- Примерочная ----------

        private static void BuildFittingRoom()
        {
            var mat = CreateMaterial("Fitting", HexColor(0x8E7FC9));
            var root = new GameObject("FittingRoom");

            CreatePanel(root.transform, "Wall_Left", mat,
                new Vector3(RoomLayout.FittingX, 1.1f, RoomLayout.FittingZ + RoomLayout.FittingD / 2f),
                new Vector3(0.06f, 2.2f, RoomLayout.FittingD));
            CreatePanel(root.transform, "Wall_Right", mat,
                new Vector3(RoomLayout.FittingX + RoomLayout.FittingW, 1.1f, RoomLayout.FittingZ + RoomLayout.FittingD / 2f),
                new Vector3(0.06f, 2.2f, RoomLayout.FittingD));
            CreatePanel(root.transform, "Wall_Back", mat,
                new Vector3(RoomLayout.FittingX + RoomLayout.FittingW / 2f, 1.1f, RoomLayout.FittingZ + RoomLayout.FittingD),
                new Vector3(RoomLayout.FittingW, 2.2f, 0.06f));

            var fittingRoom = root.AddComponent<FittingRoom>();
            var stand = new GameObject("StandPoint");
            stand.transform.SetParent(root.transform);
            stand.transform.position = RoomLayout.FittingStandPosition;
            SetPrivateField(fittingRoom, "standPoint", stand.transform);
        }

        // ---------- Курилка ----------

        private static void BuildSmokeDoor(PlayerBuffs playerBuffs)
        {
            var mat = CreateMaterial("SmokeDoor", HexColor(0x3A4150));
            var go = CreatePanel(null, "SmokeDoor", mat,
                new Vector3(RoomLayout.SmokeDoorX, 1.15f, -0.06f), new Vector3(0.96f, 2.3f, 0.12f));
            go.AddComponent<SmokeDoor>();

            var controllerGO = new GameObject("SmokeBreakController");
            var controller = controllerGO.AddComponent<SmokeBreakController>();
            SetPrivateField(controller, "playerBuffs", playerBuffs);
        }

        // ---------- Подсветка целевой ячейки для несомой коробки ----------

        /// <summary>Порт hlBox/hlArrow из MVP: зелёный маркер на полке + вращающийся индикатор
        /// над ней, показывается пока игрок несёт коробку с докстанции к назначенной ячейке.
        /// Без коллайдеров — иначе перекрыл бы raycast до самой ShelfCell под ним.</summary>
        private static void BuildCellHighlight()
        {
            var mat = CreateMaterial("CellHighlight", new Color(0.2f, 1f, 0.5f));
            var root = new GameObject("CellHighlight");
            var highlight = root.AddComponent<CellHighlight>();

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker";
            marker.transform.SetParent(root.transform);
            marker.transform.localScale = new Vector3(ShelfLength / CellsPerShelf * 0.9f, 0.06f, ShelfDepth * 0.95f);
            Object.DestroyImmediate(marker.GetComponent<BoxCollider>());
            marker.GetComponent<Renderer>().sharedMaterial = mat;
            marker.SetActive(false);

            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "Arrow";
            arrow.transform.SetParent(root.transform);
            arrow.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            arrow.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Object.DestroyImmediate(arrow.GetComponent<BoxCollider>());
            arrow.GetComponent<Renderer>().sharedMaterial = mat;
            arrow.SetActive(false);

            SetPrivateField(highlight, "marker", marker.transform);
            SetPrivateField(highlight, "arrow", arrow.transform);
        }

        // ---------- Клиенты ----------

        private static void BuildCustomerSpawner(GameObject customerPrefab)
        {
            var go = new GameObject("CustomerSpawner");
            var spawner = go.AddComponent<CustomerSpawner>();

            var entry = new GameObject("EntryPoint");
            entry.transform.SetParent(go.transform);
            entry.transform.position = new Vector3(RoomLayout.EnterX, 0f, (RoomLayout.EntranceZ0 + RoomLayout.EntranceZ1) / 2f);

            SetPrivateField(spawner, "customerPrefab", customerPrefab.GetComponent<Customer>());
            SetPrivateField(spawner, "entryPoint", entry.transform);
        }

        // ---------- Уличный декор за входом (KayKit City Builder Bits) ----------

        /// <summary>Немного уличного декора снаружи проёма для клиентов — видно через дверной
        /// проём, ощущение "снаружи есть город", а не голая пустота. Требует glTFast — без
        /// него пропы молча пропускаются (см. LoadModel), сборка не ломается.</summary>
        private static void BuildStreetDressing()
        {
            var root = new GameObject("StreetDressing");
            float doorZ = (RoomLayout.EntranceZ0 + RoomLayout.EntranceZ1) / 2f;
            float outsideX = RoomLayout.RoomWidth + 1.5f;

            PlaceStreetProp(root.transform, $"{KayKitCityFolder}/road_straight.gltf",
                new Vector3(RoomLayout.RoomWidth + 2.5f, 0f, doorZ), new Vector3(4f, 0.1f, 4f));
            PlaceStreetProp(root.transform, $"{KayKitCityFolder}/streetlight.gltf",
                new Vector3(outsideX, 0f, RoomLayout.EntranceZ0 - 1f), new Vector3(0.4f, 3f, 0.4f));
            PlaceStreetProp(root.transform, $"{KayKitCityFolder}/bench.gltf",
                new Vector3(outsideX, 0f, doorZ), new Vector3(1.4f, 0.8f, 0.6f));
            PlaceStreetProp(root.transform, $"{KayKitCityFolder}/trash_A.gltf",
                new Vector3(outsideX - 0.6f, 0f, RoomLayout.EntranceZ1 + 0.8f), new Vector3(0.5f, 0.7f, 0.5f));
        }

        private static void PlaceStreetProp(Transform parent, string modelPath, Vector3 groundPosition, Vector3 targetSize)
        {
            var visual = InstantiateModel(modelPath, parent);
            if (visual == null) return;

            visual.transform.position += new Vector3(groundPosition.x, 0f, groundPosition.z);
            FitToSizeStretch(visual, targetSize);
            GroundAt(visual, groundPosition.y);
        }

        // ---------- Префабы ----------

        private static GameObject CreateBoxPrefab()
        {
            // Корень — пустой объект с коллайдером под логику (Box.cs требует BoxCollider на
            // себе для Physics.Raycast из PlayerInteraction); визуал — отдельный дочерний
            // объект, чтобы смена модели никогда не задевала коллайдер/логику.
            var go = new GameObject("Box");
            var boxSize = new Vector3(0.4f, 0.3f, 0.4f);
            var collider = go.AddComponent<BoxCollider>();
            collider.size = boxSize;
            go.AddComponent<Box>();

            var visual = InstantiateModel(BoxModelPath, go.transform);
            if (visual != null)
            {
                FitToSizeStretch(visual, boxSize);
                CenterAt(visual, Vector3.zero);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/Box.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateCustomerPrefab()
        {
            var go = new GameObject("Customer");
            go.AddComponent<Customer>();

            // Без анимаций (Mixamo-проход ещё впереди) — просто статичный меш, который
            // едет за transform.position клиента; поворот к цели см. Customer.MoveTowards.
            var visual = InstantiateModel(CustomerModelPath, go.transform);
            if (visual != null)
            {
                FitToHeight(visual, 1.75f);
                GroundAt(visual, 0f);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/Customer.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateCourierPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Courier";
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial("CourierBody", HexColor(0xF7D046));
            go.AddComponent<Courier>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/Courier.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- Сторонние модели (Assets/ThirdPatry) ----------

        /// <summary>Грузит GameObject-ассет модели (FBX/OBJ). Логирует ошибку, если путь битый
        /// (ассет переименовали/удалили) — иначе сцена молча соберётся без части геометрии.</summary>
        private static GameObject LoadModel(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                Debug.LogError($"Не найдена модель по пути '{path}' — проверь, что ассет всё ещё лежит там же.");
            return asset;
        }

        /// <summary>Инстанцирует модель как дочерний объект в (0,0,0)/identity/scale=1 —
        /// дальше вызывающий код сам подгоняет масштаб и позицию под нужный footprint.</summary>
        private static GameObject InstantiateModel(string modelPath, Transform parent)
        {
            var model = LoadModel(modelPath);
            if (model == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Vector3 MeasureBoundsSize(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = new Bounds(instance.transform.position, Vector3.zero);
                return Vector3.zero;
            }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.size;
        }

        /// <summary>Растягивает модель по каждой оси независимо, чтобы точно попасть в
        /// targetSize — годится для пропов (коробка/стеллаж), где важен игровой footprint,
        /// а не оригинальные пропорции ассета. Снимает необходимость знать заранее, в каких
        /// единицах измерения экспортирован конкретный ассет-пак.</summary>
        private static void FitToSizeStretch(GameObject instance, Vector3 targetSize)
        {
            var size = MeasureBoundsSize(instance, out _);
            var scale = instance.transform.localScale;
            instance.transform.localScale = new Vector3(
                size.x > 0.0001f ? scale.x * targetSize.x / size.x : scale.x,
                size.y > 0.0001f ? scale.y * targetSize.y / size.y : scale.y,
                size.z > 0.0001f ? scale.z * targetSize.z / size.z : scale.z);
        }

        /// <summary>Равномерно масштабирует модель так, чтобы её высота совпала с targetHeight,
        /// сохраняя пропорции — обязательно для персонажей, иначе гуманоид расплющится.</summary>
        private static void FitToHeight(GameObject instance, float targetHeight)
        {
            var size = MeasureBoundsSize(instance, out _);
            if (size.y <= 0.0001f) return;
            instance.transform.localScale *= targetHeight / size.y;
        }

        /// <summary>Сдвигает инстанс по мировой Y так, чтобы низ его фактического Bounds лёг
        /// на floorLocalY — не важно, где у исходной модели пивот (пол/центр/что угодно).</summary>
        private static void GroundAt(GameObject instance, float floorLocalY)
        {
            MeasureBoundsSize(instance, out var bounds);
            instance.transform.position += new Vector3(0f, floorLocalY - bounds.min.y, 0f);
        }

        /// <summary>Центрирует Bounds инстанса на заданной локальной точке родителя — годится
        /// для пропов вроде коробки, чей коллайдер тоже стоит центром в (0,0,0).</summary>
        private static void CenterAt(GameObject instance, Vector3 parentLocalPoint)
        {
            MeasureBoundsSize(instance, out var bounds);
            var worldTarget = instance.transform.parent != null
                ? instance.transform.parent.TransformPoint(parentLocalPoint)
                : parentLocalPoint;
            instance.transform.position += worldTarget - bounds.center;
        }

        // ---------- Утилиты ----------

        private static GameObject CreatePanel(Transform parent, string name, Material material, Vector3 localPosition, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;

            return go;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            EnsureFolder(MaterialFolder);

            var path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Color HexColor(int hex)
        {
            float r = ((hex >> 16) & 0xFF) / 255f;
            float g = ((hex >> 8) & 0xFF) / 255f;
            float b = (hex & 0xFF) / 255f;
            return new Color(r, g, b);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        /// <summary>Ставит значение в приватное [SerializeField]-поле через SerializedObject —
        /// так же, как если бы вы сами перетащили ссылку в инспекторе.</summary>
        private static void SetPrivateField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"Поле '{fieldName}' не найдено на {target.GetType().Name}");
                return;
            }

            switch (value)
            {
                case Object unityObj:
                    prop.objectReferenceValue = unityObj;
                    break;
                case float f:
                    prop.floatValue = f;
                    break;
                case string s:
                    prop.stringValue = s;
                    break;
                case int i:
                    prop.intValue = i;
                    break;
                default:
                    Debug.LogError($"Неизвестный тип значения для поля '{fieldName}'");
                    return;
            }

            so.ApplyModifiedProperties();
        }
    }
}
