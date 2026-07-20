using Gazon.Core;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>Спавнит коробки-возвраты на стойке у случайного окна (после примерочной — см. Customer).</summary>
    public class ReturnSpawner : MonoBehaviour
    {
        public static ReturnSpawner Instance { get; private set; }

        [SerializeField] private Box boxPrefab;

        private void Awake()
        {
            Instance = this;
        }

        public void SpawnReturn()
        {
            float wz = RoomLayout.WindowZ[Random.value < 0.5f ? 0 : 1];
            var pos = new Vector3(
                RoomLayout.CounterX + RoomLayout.CounterW / 2f + 0.3f,
                0.9f,
                wz + Random.Range(0.6f, 1.2f));
            var box = Instantiate(boxPrefab, pos, Quaternion.identity);
            box.PlaceAsCounterReturn();
        }
    }
}
