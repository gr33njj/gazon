using Gazon.Core;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>
    /// Докстанция: раз в интервал (зависит от дня) выгружает партию коробок. 30% партии — хрупкое,
    /// 40% из хрупких — помятое (штраф при укладке, см. ShelfCell). Если в партии была хотя бы одна
    /// хрупкая коробка — вызывает курьера с претензией (см. CourierSpawner).
    /// </summary>
    public class DockSpawner : MonoBehaviour
    {
        [SerializeField] private Box boxPrefab;
        [SerializeField] private Vector2 dockSize = new Vector2(3.8f, 6.0f);
        [SerializeField] private CourierSpawner courierSpawner;

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
            bool hadFragile = false;

            for (int i = 0; i < count; i++)
            {
                var pos = transform.position + new Vector3(
                    Random.Range(0.4f, dockSize.x - 0.4f),
                    0.18f,
                    Random.Range(0.4f, dockSize.y - 0.4f));
                var box = Instantiate(boxPrefab, pos, Quaternion.identity);
                box.PlaceOnDock();

                bool fragile = Random.value < 0.3f;
                bool dented = fragile && Random.value < 0.4f;
                box.ConfigureDelivery(fragile, dented);
                if (fragile) hadFragile = true;
            }

            if (hadFragile && courierSpawner != null)
            {
                courierSpawner.SpawnCourier();
                GameManager.Instance.Toast("📦🔥 Кажется, «хрупкое» летело особенно красиво. Курьер с вопросом.", "warn");
            }
        }
    }
}
