using System.IO;
using Gazon.Core;
using Gazon.Customers;
using Gazon.Player;
using Gazon.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gazon.EditorTools
{
    /// <summary>
    /// Собирает минимальную сцену вертикального среза кодом через UnityEditor API — вместо
    /// ручной сборки объект-за-объектом (см. Assets/Scripts/README.md, тот же чек-лист, но кодом).
    /// Запускать на ПУСТОЙ новой сцене (File → New Scene), иначе объекты задвоятся с уже
    /// существующими. Меню: Gazon → Собрать сцену вертикального среза.
    /// </summary>
    public static class SceneBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string MaterialFolder = "Assets/Materials";

        // Стеллажи: см. Design/World/pvz_layout.md — 3 шт. (А/Б/В), длина 6, глубина 0.7,
        // 8 ячеек на стеллаж. Расстановка в комнате (кто где стоит по X/Z) — решение SceneBuilder'а,
        // в layout-доке её нет; при достройке стен (Phase 2) проверить на глаз с реальным персонажем.
        private static readonly string[] ShelfLetters = { "А", "Б", "В" };
        private const int CellsPerShelf = 8;
        private const float ShelfLength = 6f;
        private const float ShelfDepth = 0.7f;
        private const float ShelfCellHeight = 0.8f;
        private const float ShelfBottomY = 0.9f;
        private const float BoardThickness = 0.05f;
        private const float PostThickness = 0.08f;
        private const float ShelfCenterX = -6f;
        private const float ShelfFirstRowZ = -3.2f;
        private const float ShelfRowSpacing = 2.5f;

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

            BuildPlayer();
            BuildGameManager();
            BuildFloor();
            BuildDock(boxPrefab);
            BuildShelfCells();
            var windows = BuildWindows();
            BuildCustomerSpawner(customerPrefab, windows);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Сцена собрана. Сохрани её (Ctrl+S) и нажми Play.");
        }

        private static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, -2f);

            player.AddComponent<CharacterController>();
            var interaction = player.AddComponent<PlayerInteraction>();
            var controller = player.AddComponent<PlayerController>();

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

        private static void BuildGameManager()
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            go.AddComponent<DebugHud>();
        }

        private static void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
        }

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

        private static void BuildDock(GameObject boxPrefab)
        {
            var dock = new GameObject("Dock");
            dock.transform.position = new Vector3(0f, 0f, 5f);
            var spawner = dock.AddComponent<DockSpawner>();
            SetPrivateField(spawner, "boxPrefab", boxPrefab.GetComponent<Box>());
        }

        /// <summary>3 стеллажа (А/Б/В) по 8 ячеек = 24 ячейки, каждый — с видимым каркасом
        /// (стойки, полки, перегородки), а не голыми пустыми GameObject'ами.</summary>
        private static void BuildShelfCells()
        {
            var shelvesRoot = new GameObject("Shelves");
            var shelfMaterial = CreateShelfMaterial();

            for (int s = 0; s < ShelfLetters.Length; s++)
            {
                var z = ShelfFirstRowZ + s * ShelfRowSpacing;
                BuildShelfUnit(shelvesRoot.transform, ShelfLetters[s], new Vector3(ShelfCenterX, 0f, z), shelfMaterial);
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

            // 9 стоек на 8 ячеек (края + перегородки между ними).
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

        private static Material CreateShelfMaterial()
        {
            EnsureFolder(MaterialFolder);

            var path = $"{MaterialFolder}/ShelfWood.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "ShelfWood", color = new Color(0.5f, 0.33f, 0.2f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static WindowStation[] BuildWindows()
        {
            var windows = new WindowStation[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"Window{i + 1}");
                go.transform.position = new Vector3(4f, 1f, i * 2f);
                var window = go.AddComponent<WindowStation>();

                var stand = new GameObject("StandPoint");
                stand.transform.SetParent(go.transform);
                stand.transform.localPosition = new Vector3(0f, 0f, -0.5f);

                SetPrivateField(window, "customerStandPoint", stand.transform);
                windows[i] = window;
            }
            return windows;
        }

        private static void BuildCustomerSpawner(GameObject customerPrefab, WindowStation[] windows)
        {
            var go = new GameObject("CustomerSpawner");
            var spawner = go.AddComponent<CustomerSpawner>();

            var entry = new GameObject("EntryPoint");
            entry.transform.position = new Vector3(-3f, 0f, 0f);

            SetPrivateField(spawner, "customerPrefab", customerPrefab.GetComponent<Customer>());
            SetPrivateField(spawner, "entryPoint", entry.transform);
            SetPrivateField(spawner, "exitPositionZ", -5f);
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
