using Gazon.Core;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>
    /// Спавнит курьера у входа при партии с хрупким товаром (см. DockSpawner). Если игрок не
    /// подходит достаточно долго — курьер сам уходит, стрелка отменяется без последствий.
    /// (В MVP это окно было завязано на косметическую анимацию газели ~3.6 сек — здесь она не
    /// портирована, поэтому таймаут сделан щедрее, чтобы у игрока был реальный шанс успеть.)
    /// </summary>
    public class CourierSpawner : MonoBehaviour
    {
        public static CourierSpawner Instance { get; private set; }

        [SerializeField] private Courier courierPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float patienceSeconds = 25f;

        private Courier current;
        private float timer;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (current == null) return;
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Play) return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                GameManager.Instance.Toast("🚚 Курьер уехал. Стрелка отменяется.", "warn");
                Despawn();
            }
        }

        public void SpawnCourier()
        {
            if (current != null) return; // уже кто-то ждёт разборок
            current = Instantiate(courierPrefab, spawnPoint.position, spawnPoint.rotation);
            timer = patienceSeconds;
        }

        public void ResolveCourier()
        {
            Despawn();
        }

        private void Despawn()
        {
            if (current != null) Destroy(current.gameObject);
            current = null;
        }
    }
}
