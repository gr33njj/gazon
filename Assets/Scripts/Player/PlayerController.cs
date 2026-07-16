using UnityEngine;

namespace Gazon.Player
{
    /// <summary>
    /// FPS-движение как в MVP: WASD, Shift — бег, мышь — осмотр (требует CharacterController на объекте).
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
        private CharacterController controller;

        private void Awake()
        {
            interaction = GetComponent<PlayerInteraction>();
            controller = GetComponent<CharacterController>();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
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

            controller.SimpleMove(move * speed);
        }
    }
}
