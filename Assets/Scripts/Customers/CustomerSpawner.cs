using Gazon.Core;
using Gazon.World;
using UnityEngine;

namespace Gazon.Customers
{
    /// <summary>Спавнит клиентов у входа, только если есть свободное окно и незаказанная коробка на полке.</summary>
    public class CustomerSpawner : MonoBehaviour
    {
        [SerializeField] private Customer customerPrefab;
        [SerializeField] private Transform entryPoint;
        [SerializeField] private float exitPositionZ;
        [SerializeField] private int maxActiveCustomers = 8;

        private float spawnTimer = 5f; // MVP: G.custT стартует с 5

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Play)
                return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                TrySpawn();
                spawnTimer = GameManager.CustomerSpawnIntervalForDay(GameManager.Instance.Day);
            }
        }

        private void TrySpawn()
        {
            if (Customer.ActiveCount >= maxActiveCustomers) return;

            var box = Box.FindAvailableOrderableBox();
            if (box == null) return;

            var window = WindowStation.FindFreeWindow();
            if (window == null) return;

            var customer = Instantiate(customerPrefab, entryPoint.position, Quaternion.identity);
            float basePatience = GameManager.BasePatienceForDay(GameManager.Instance.Day);
            customer.Initialize(box, window, basePatience, exitPositionZ);
        }
    }
}
