using Gazon.Core;
using UnityEngine;

namespace Gazon.Player
{
    /// <summary>
    /// FPS-движение как в MVP: WASD, Shift — бег, мышь — осмотр (требует CharacterController на объекте).
    /// Скорость модифицируется бустом с перекура (×1.25) и апгрейдом "микрофон" (×1.05) — см. GameManager/PlayerBuffs.
    /// Движение и осмотр отключаются, пока игра не в состоянии Play или открыта модальная панель (InputLock).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float runMultiplier = 1.5f;
        [SerializeField] private float mouseSensitivity = 0.13f;
        [SerializeField] private float carrySpeedMultiplier = 3.4f / 4.2f; // MVP: 3.4 при carry vs 4.2 без

        private float pitch;
        private PlayerInteraction interaction;
        private PlayerBuffs buffs;
        private CharacterController controller;

        private void Awake()
        {
            interaction = GetComponent<PlayerInteraction>();
            buffs = GetComponent<PlayerBuffs>();
            controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged.AddListener(OnStateChanged);
        }

        private void OnStateChanged(GameState state)
        {
            Cursor.lockState = state == GameState.Play ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = state != GameState.Play;
        }

        private bool CanAct => GameManager.Instance != null
            && GameManager.Instance.State == GameState.Play
            && !InputLock.AnyOpen;

        private void Update()
        {
            if (!CanAct) return;
            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mx);
            pitch = Mathf.Clamp(pitch - my, -80f, 80f);
            if (playerCamera != null)
                playerCamera.transform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        private void HandleMove()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            var move = (transform.right * h + transform.forward * v);
            if (move.sqrMagnitude > 1f) move.Normalize();

            float speed = walkSpeed;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= runMultiplier;
            if (interaction != null && interaction.CarriedBox != null) speed *= carrySpeedMultiplier;
            if (buffs != null && buffs.HasSpeedBuff) speed *= 1.25f;
            if (GameManager.Instance != null && GameManager.Instance.MicUpgrade) speed *= 1.05f;

            controller.SimpleMove(move * speed);
        }
    }
}
