using Gazon.Core;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>
    /// Перекур: фиксированная длительность 4 сек, кулдаун 45 сек после. 55% — бонус (скорость ×1.25
    /// на 40 сек + дофамин), 45% — штраф 60₽ и −0.2★. Блокирует движение/интеракцию на время (InputLock).
    /// </summary>
    public class SmokeBreakController : MonoBehaviour
    {
        public static SmokeBreakController Instance { get; private set; }

        [SerializeField] private PlayerBuffs playerBuffs;
        private const float Duration = 4f;
        private float elapsed;

        public bool IsActive { get; private set; }
        public float Progress01 => Mathf.Clamp01(elapsed / Duration);
        public float CooldownRemaining => playerBuffs != null ? playerBuffs.SmokeCooldown : 0f;
        public bool CanStart => !IsActive && CooldownRemaining <= 0f;

        private void Awake()
        {
            Instance = this;
        }

        public void StartBreak()
        {
            if (!CanStart) return;
            IsActive = true;
            elapsed = 0f;
            InputLock.SmokeActive = true;
            GameManager.Instance.Toast("🚬 ПВЗ «закрыт». Очередь в ярости копится.", "warn");
        }

        private void Update()
        {
            if (!IsActive) return;
            elapsed += Time.deltaTime;
            if (elapsed >= Duration)
                EndBreak();
        }

        private void EndBreak()
        {
            IsActive = false;
            InputLock.SmokeActive = false;
            playerBuffs.ResetSmokeCooldown(45f);

            if (Random.value < 0.55f)
            {
                playerBuffs.GrantSpeedBuff(40f);
                playerBuffs.GrantDopamine(30f);
                GameManager.Instance.Toast("⚡ Энергетик + ашкудэ зашли идеально. Скорость и дофамин!", "good");
            }
            else
            {
                GameManager.Instance.Fine(60f, "Спалили на перекуре. Жалоба в поддержку");
                GameManager.Instance.AddRating(-0.2f);
            }
        }
    }
}
