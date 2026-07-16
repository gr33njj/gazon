using Gazon.Core;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>Докстанция: раз в интервал (зависит от дня) выгружает партию коробок.</summary>
    public class DockSpawner : MonoBehaviour
    {
        [SerializeField] private Box boxPrefab;
        [SerializeField] private Vector2 dockSize = new Vector2(3.8f, 6.0f);
        private float truckTimer;

        private void Start()
        {
            truckTimer = 1f; // MVP: G.truckT стартует с 1.0, чтобы первая партия приехала быстро
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Play)
                return;

            truckTimer -= Time.deltaTime;
            if (truckTimer <= 0f)
            {
                SpawnBatch();
                truckTimer = GameManager.TruckIntervalForDay(GameManager.Instance.Day);
            }
        }

        private void SpawnBatch()
        {
            int count = GameManager.BoxesPerTruckForDay(GameManager.Instance.Day);
            for (int i = 0; i < count; i++)
            {
                var pos = transform.position + new Vector3(
                    Random.Range(0.4f, dockSize.x - 0.4f),
                    0.18f,
                    Random.Range(0.4f, dockSize.y - 0.4f));
                var box = Instantiate(boxPrefab, pos, Quaternion.identity);
                box.PlaceOnDock();
            }
        }
    }
}
