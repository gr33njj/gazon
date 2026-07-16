using System.Collections.Generic;
using Gazon.Core;
using Gazon.Customers;
using Gazon.Interaction;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>Окно выдачи (в MVP их 2). Игрок приносит сюда коробку клиента, который у окна ждёт.</summary>
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
            var box = player.CarriedBox;
            if (box == null || CurrentCustomer == null) return string.Empty;
            if (CurrentCustomer.OrderBox != box) return "Это не его заказ";
            return "Выдать заказ";
        }

        public void Interact(PlayerInteraction player)
        {
            var box = player.CarriedBox;
            if (box == null || CurrentCustomer == null || CurrentCustomer.OrderBox != box) return;

            box.HandOver();
            player.DropCarriedBox(destroyBox: true);
            GameManager.Instance.AddMoney(45f);
            CurrentCustomer.CompleteOrderAndLeave();
        }
    }
}
