using System;
using Gazon.Interaction;
using Gazon.World;
using UnityEngine;
using UnityEngine.Events;

namespace Gazon.Player
{
    // UnityEvent<T> сам по себе не показывается в инспекторе — нужен конкретный сериализуемый подкласс.
    [Serializable] public class StringEvent : UnityEvent<string> { }

    /// <summary>Raycast вперёд из камеры, поиск IInteractable, E — интеракция, перенос коробки в руках.</summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform handAnchor;
        [SerializeField] private float interactRange = 2.9f;
        [SerializeField] private LayerMask interactableMask = ~0;

        public StringEvent OnPromptChanged;

        public Box CarriedBox { get; private set; }

        private IInteractable currentTarget;

        private void Update()
        {
            currentTarget = FindTarget();
            string prompt = currentTarget?.GetPrompt(this) ?? string.Empty;
            OnPromptChanged?.Invoke(prompt);

            if (Input.GetKeyDown(KeyCode.E) && currentTarget != null && !string.IsNullOrEmpty(prompt))
            {
                currentTarget.Interact(this);
            }
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
    }
}
