using UnityEngine;

namespace Gazon.Player
{
    /// <summary>
    /// Временные баффы игрока — 1:1 из MVP (P.dopamine/P.speed/P.smiling, smokeCd):
    /// дофамин от рилсов открывает улыбку [R] на 10 сек; спидбуст — награда за удачный перекур;
    /// кулдаун перекура — 45 сек после выхода.
    /// </summary>
    public class PlayerBuffs : MonoBehaviour
    {
        public static PlayerBuffs Instance { get; private set; }

        [SerializeField] private float dopamine;
        [SerializeField] private float speedBuff;
        [SerializeField] private float smiling;
        [SerializeField] private float smokeCooldown;

        private void Awake()
        {
            Instance = this;
        }

        public float Dopamine => dopamine;
        public float SpeedBuff => speedBuff;
        public float Smiling => smiling;
        public float SmokeCooldown => smokeCooldown;
        public bool IsSmiling => smiling > 0f;
        public bool HasSpeedBuff => speedBuff > 0f;

        private void Update()
        {
            if (dopamine > 0f) dopamine -= Time.deltaTime;
            if (speedBuff > 0f) speedBuff -= Time.deltaTime;
            if (smiling > 0f) smiling -= Time.deltaTime;
            if (smokeCooldown > 0f) smokeCooldown -= Time.deltaTime;
        }

        public void GrantDopamine(float seconds) => dopamine = Mathf.Max(dopamine, seconds);

        public void GrantSpeedBuff(float seconds) => speedBuff = seconds;

        /// <summary>MVP: клавиша R — улыбка держится 10 сек, доступна только пока есть дофамин.</summary>
        public bool TrySmile()
        {
            if (dopamine <= 0f) return false;
            smiling = 10f;
            return true;
        }

        public void ResetSmokeCooldown(float seconds) => smokeCooldown = seconds;

        public void ResetForNewGame()
        {
            dopamine = 0f;
            speedBuff = 0f;
            smiling = 0f;
            smokeCooldown = 0f;
        }
    }
}
