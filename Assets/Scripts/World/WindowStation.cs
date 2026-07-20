using System.Collections.Generic;
using Gazon.Core;
using Gazon.Customers;
using Gazon.Interaction;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>
    /// Окно выдачи (в MVP их 2). Без коробки в руках — можно поиграть в крокодила/пробить в
    /// «Глазе Бога». С коробкой заказа — либо сначала «Глаз Бога» (если код не восстановлен),
    /// либо попытка выдачи (30% шанс QTE, см. MinigameController).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class WindowStation : MonoBehaviour, IInteractable
    {
        private static readonly List<WindowStation> AllWindows = new List<WindowStation>();

        [SerializeField] private Transform customerStandPoint;

        public Customer CurrentCustomer { get; private set; }
        public Transform CustomerStandPoint => customerStandPoint != null ? customerStandPoint : transform;
        public bool IsFree => CurrentCustomer == null;

        private void OnEnable() => AllWindows.Add(this);
        private void OnDisable() => AllWindows.Remove(this);

        public static WindowStation FindFreeWindow()
        {
            foreach (var window in AllWindows)
                if (window.IsFree)
                    return window;
            return null;
        }

        public void AssignCustomer(Customer customer)
        {
            CurrentCustomer = customer;
        }

        public void ClearCustomer(Customer customer)
        {
            if (CurrentCustomer == customer) CurrentCustomer = null;
        }

        public string GetPrompt(PlayerInteraction player)
        {
            if (CurrentCustomer == null) return string.Empty;
            var box = player.CarriedBox;

            if (box == null)
            {
                if (CurrentCustomer.Archetype == CustomerArchetype.Babka && !CurrentCustomer.Guessed)
                    return "Играть в «крокодила» с бабушкой";
                if (CurrentCustomer.NeedsCode && !CurrentCustomer.CodeFixed)
                    return "Пробить в «Глазе Бога»";
                return string.Empty;
            }

            if (CurrentCustomer.OrderBox != box) return string.Empty;

            if (CurrentCustomer.NeedsCode && !CurrentCustomer.CodeFixed)
                return "Пробить в «Глазе Бога»";

            return "Выдать заказ";
        }

        public void Interact(PlayerInteraction player)
        {
            if (CurrentCustomer == null) return;
            var box = player.CarriedBox;

            if (box == null)
            {
                if (CurrentCustomer.Archetype == CustomerArchetype.Babka && !CurrentCustomer.Guessed)
                {
                    MinigameController.Instance.StartBabka(CurrentCustomer);
                    return;
                }
                if (CurrentCustomer.NeedsCode && !CurrentCustomer.CodeFixed)
                {
                    MinigameController.Instance.StartEye(CurrentCustomer);
                    return;
                }
                return;
            }

            if (CurrentCustomer.OrderBox != box) return;

            if (CurrentCustomer.NeedsCode && !CurrentCustomer.CodeFixed)
            {
                MinigameController.Instance.StartEye(CurrentCustomer);
                return;
            }

            // MVP: attemptHandOver — 30% шанс QTE перед выдачей.
            if (Random.value < 0.3f)
                MinigameController.Instance.StartQte(this, CurrentCustomer, box);
            else
                CompleteHandOver(box);
        }

        /// <summary>
        /// Вызывается напрямую (мгновенная выдача) либо из MinigameController после QTE
        /// (штраф за помятость при провале QTE уже применён вызывающей стороной).
        /// </summary>
        public void CompleteHandOver(Box box)
        {
            var customer = CurrentCustomer;
            box.HandOver();
            PlayerInteraction.Instance.DropCarriedBox(destroyBox: true);
            GameManager.Instance.Earn(45f);
            GameManager.Instance.RecordServed();
            GameManager.Instance.Toast("✅ Выдано! +45 ₽");
            customer?.CompleteOrderAndLeave();
        }
    }
}
