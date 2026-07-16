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

        private static void BuildShelfCells()
        {
            var labels = new[] { "A1", "A2", "A3" };
            for (int i = 0; i < labels.Length; i++)
            {
                var go = new GameObject($"ShelfCell_{labels[i]}");
                go.transform.position = new Vector3(2f + i * 0.6f, 1f, 0f);
                var cell = go.AddComponent<ShelfCell>();
                SetPrivateField(cell, "label", labels[i]);
            }
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
