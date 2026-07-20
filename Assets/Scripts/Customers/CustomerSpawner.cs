using Gazon.Core;
using Gazon.World;
using UnityEngine;

namespace Gazon.Customers
{
    /// <summary>
    /// Спавнит клиентов у входа, если есть незаказанная коробка на полке (окно свободно не обязательно —
    /// клиенты теперь встают в очередь, см. Customer.CustomerState.Queue). Архетип — взвешенный ролл
    /// по весам из ContentDatabase (customer_archetypes), 1:1 с MVP (roll&lt;0.10 бабушка, &lt;0.19 шопоголичка).
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        private static readonly string[] ColorNames =
        {
            "красной", "синей", "зелёной", "жёлтой", "розовой", "фиолетовой"
        };

        [SerializeField] private Customer customerPrefab;
        [SerializeField] private Transform entryPoint;
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

            var archetype = RollArchetype();
            string colorName = ColorNames[Random.Range(0, ColorNames.Length)];
            bool needsCode = archetype == CustomerArchetype.Normal && Random.value < 0.22f;
            var item = archetype == CustomerArchetype.Babka ? ContentDatabase.RandomBabkaItem() : default;
            float basePatience = ArchetypeBasePatience(archetype) - Mathf.Min(GameManager.Instance.Day * 1.5f, 10f);

            var customer = Instantiate(customerPrefab, entryPoint.position, Quaternion.identity);

            int queueIndex = 0;
            foreach (var c in Customer.Active)
                if (c != customer && c.State == CustomerState.Queue) queueIndex++;

            customer.Initialize(box, archetype, basePatience, colorName, needsCode, item, queueIndex);
        }

        private static CustomerArchetype RollArchetype()
        {
            float roll = Random.value;
            float babkaWeight = ArchetypeWeight("babka");
            float shopaholicWeight = ArchetypeWeight("shopaholic");

            if (roll < babkaWeight) return CustomerArchetype.Babka;
            if (roll < babkaWeight + shopaholicWeight) return CustomerArchetype.Shopaholic;
            return CustomerArchetype.Normal;
        }

        private static float ArchetypeWeight(string name)
        {
            foreach (var a in ContentDatabase.CustomerArchetypes)
                if (a.name == name) return a.spawnWeight;
            return 0f;
        }

        private static float ArchetypeBasePatience(CustomerArchetype archetype)
        {
            string name = archetype.ToDbName();
            foreach (var a in ContentDatabase.CustomerArchetypes)
                if (a.name == name) return a.basePatience;
            return 34f;
        }
    }
}
