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

        private const int CellsPerShelf = RoomLayout.CellsPerRack;
        private const float ShelfLength = RoomLayout.RackLength;
        private const float ShelfDepth = RoomLayout.RackDepth;
        private const float ShelfCellHeight = 0.8f;
        private const float ShelfBottomY = 0.9f;
        private const float BoardThickness = 0.05f;
        private const float PostThickness = 0.08f;

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
            BuildRoom();
            BuildShelves();
            var courierSpawner = BuildCourierSpawner(courierPrefab);
            BuildDock(boxPrefab, courierSpawner);
            BuildCounterAndWindows();
            BuildReturnsTable(boxPrefab);
            BuildFittingRoom();
            BuildSmokeDoor(player.GetComponent<PlayerBuffs>());
            BuildCustomerSpawner(customerPrefab);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Сцена собрана. Сохрани её (Ctrl+S) и нажми Play.");
        }

        // ---------- Игрок и менеджеры ----------

        private static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(13f, 1f, 11.5f); // MVP: P.x=13,z=11.5

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
            camGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
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
            var shelfMaterial = CreateMaterial("ShelfWood", new Color(0.5f, 0.33f, 0.2f));

            foreach (var rack in RoomLayout.Racks)
            {
                var center = new Vector3(rack.X0 + ShelfLength / 2f, 0f, rack.Z + ShelfDepth / 2f);
                BuildShelfUnit(shelvesRoot.transform, rack.Label, center, shelfMaterial);
            }
        }

        private static void BuildShelfUnit(Transform parent, string letter, Vector3 position, Material material)
        {
            var shelfRoot = new GameObject($"Shelf_{letter}");
            shelfRoot.transform.SetParent(parent);
            shelfRoot.transform.position = position;

            var cellWidth = ShelfLength / CellsPerShelf;
            var halfLength = ShelfLength / 2f;
            var topY = ShelfBottomY + ShelfCellHeight;
            var midY = (ShelfBottomY + topY) / 2f;

            CreatePanel(shelfRoot.transform, "BackPanel", material,
                new Vector3(0f, midY, -ShelfDepth / 2f), new Vector3(ShelfLength, ShelfCellHeight, BoardThickness));
            CreatePanel(shelfRoot.transform, "BottomBoard", material,
                new Vector3(0f, ShelfBottomY, 0f), new Vector3(ShelfLength, BoardThickness, ShelfDepth));
            CreatePanel(shelfRoot.transform, "TopBoard", material,
                new Vector3(0f, topY, 0f), new Vector3(ShelfLength, BoardThickness, ShelfDepth));

            for (int i = 0; i <= CellsPerShelf; i++)
            {
                var postX = -halfLength + i * cellWidth;
                var name = i == 0 ? "PostStart" : i == CellsPerShelf ? "PostEnd" : $"Divider{i}";
                CreatePanel(shelfRoot.transform, name, material,
                    new Vector3(postX, midY, 0f), new Vector3(PostThickness, ShelfCellHeight, ShelfDepth));
                CreatePanel(shelfRoot.transform, $"{name}_Leg", material,
                    new Vector3(postX, ShelfBottomY / 2f, 0f), new Vector3(PostThickness, ShelfBottomY, PostThickness));
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
            var go = new GameObject($"Sign_{letter}");
            go.transform.SetParent(parent);
            go.transform.localPosition = new Vector3(0f, topY + 0.3f, ShelfDepth / 2f);

            var textMesh = go.AddComponent<TextMesh>();
            textMesh.text = letter;
            textMesh.characterSize = 0.3f;
            textMesh.fontSize = 48;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.color = Color.white;
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

        // ---------- Префабы ----------

        private static GameObject CreateBoxPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Box";
            go.transform.localScale = new Vector3(0.4f, 0.3f, 0.4f);
            go.AddComponent<Box>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/Box.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateCustomerPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Customer";
            go.AddComponent<Customer>();

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
