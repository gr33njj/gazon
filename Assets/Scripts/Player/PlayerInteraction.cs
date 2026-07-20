using System;
using Gazon.Core;
using Gazon.Customers;
using Gazon.Interaction;
using Gazon.World;
using UnityEngine;
using UnityEngine.Events;

namespace Gazon.Player
{
    // UnityEvent<T> сам по себе не показывается в инспекторе — нужен конкретный сериализуемый подкласс.
    [Serializable] public class StringEvent : UnityEvent<string> { }

    /// <summary>
    /// Raycast вперёд из камеры, поиск IInteractable, E — интеракция (мгновенная либо удержание
    /// для IHoldInteractable — см. стол возвратов), перенос коробки в руках, кража (X).
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        public static PlayerInteraction Instance { get; private set; }

        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform handAnchor;
        [SerializeField] private float interactRange = 2.9f;
        [SerializeField] private LayerMask interactableMask = ~0;

        public StringEvent OnPromptChanged;

        public Box CarriedBox { get; private set; }
        public float HoldProgress01 { get; private set; }
        public bool IsHolding { get; private set; }

        private IInteractable currentTarget;
        private float holdTimer;

        private void Awake()
        {
            Instance = this;
        }

        private bool CanAct => GameManager.Instance != null
            && GameManager.Instance.State == GameState.Play
            && !InputLock.AnyOpen;

        private void Update()
        {
            if (!CanAct)
            {
                currentTarget = null;
                OnPromptChanged?.Invoke(string.Empty);
                ResetHold();
                return;
            }

            currentTarget = FindTarget();
            string prompt = currentTarget?.GetPrompt(this) ?? string.Empty;
            OnPromptChanged?.Invoke(prompt);

            if (string.IsNullOrEmpty(prompt))
            {
                ResetHold();
                return;
            }

            if (currentTarget is IHoldInteractable holdable && holdable.HoldDuration > 0f)
                HandleHold(holdable);
            else if (Input.GetKeyDown(KeyCode.E))
                currentTarget.Interact(this);
        }

        private void HandleHold(IHoldInteractable holdable)
        {
            if (Input.GetKey(KeyCode.E))
            {
                IsHolding = true;
                holdTimer += Time.deltaTime;
                HoldProgress01 = Mathf.Clamp01(holdTimer / holdable.HoldDuration);
                if (holdTimer >= holdable.HoldDuration)
                {
                    ResetHold();
                    holdable.Interact(this);
                }
            }
            else
            {
                ResetHold();
            }
        }

        private void ResetHold()
        {
            IsHolding = false;
            holdTimer = 0f;
            HoldProgress01 = 0f;
        }

        private IInteractable FindTarget()
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out var hit, interactRange, interactableMask))
                return hit.collider.GetComponentInParent<IInteractable>();
            return null;
        }

        public void PickUpBox(Box box)
        {
            CarriedBox = box;
            box.transform.SetParent(handAnchor);
            box.transform.localPosition = Vector3.zero;
            box.transform.localRotation = Quaternion.identity;
        }

        public void DropCarriedBox(bool destroyBox = false)
        {
            if (CarriedBox == null) return;
            var box = CarriedBox;
            CarriedBox = null;
            box.transform.SetParent(null);
            if (destroyBox) Destroy(box.gameObject);
        }

        /// <summary>
        /// MVP: клавиша X — целится в незаказанную коробку на полке. Шанс поймать:
        /// 0.18 + 0.06 * (клиентов в зале). Поимка = мгновенное увольнение, успех = +1 чехол в инвентарь.
        /// </summary>
        public void TrySteal()
        {
            if (!CanAct || CarriedBox != null) return;

            var target = FindTarget();
            if (target is ShelfCell cell && cell.CurrentBox != null)
                target = cell.CurrentBox;

            if (target is not Box box || box.State != BoxState.OnShelf || box.OrderedBy != null || box.IsReturn)
            {
                GameManager.Instance.Toast("Это спиздить не получится. Пока.", "warn");
                return;
            }

            int inHall = Customer.ActiveCount;
            float catchChance = 0.18f + 0.06f * inHall;
            if (UnityEngine.Random.value < catchChance)
            {
                GameManager.Instance.Fire("Служба безопасности видела, как вы «переставляли» коробку себе в рюкзак.");
                return;
            }

            box.AssignedCell?.Release();
            Destroy(box.gameObject);
            GameManager.Instance.RecordStolenItem();
            GameManager.Instance.Toast("🤫 Внутри был чехол на Poco X7 Pro Max 48px. Классика.", "good");
        }
    }
}
