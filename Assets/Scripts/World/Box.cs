using System.Collections.Generic;
using Gazon.Customers;
using Gazon.Interaction;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    public enum BoxState
    {
        Dock,
        Carried,
        OnShelf,
        HandedOver
    }

    /// <summary>Коробка. Жизненный цикл: Dock -> Carried -> OnShelf -> Carried -> HandedOver.</summary>
    public class Box : MonoBehaviour, IInteractable
    {
        private static readonly List<Box> AllBoxes = new List<Box>();

        [SerializeField] private BoxState state = BoxState.Dock;
        [SerializeField] private ShelfCell assignedCell;

        public BoxState State => state;
        public ShelfCell AssignedCell => assignedCell;
        public Customer OrderedBy { get; set; }

        private void OnEnable() => AllBoxes.Add(this);
        private void OnDisable() => AllBoxes.Remove(this);

        /// <summary>MVP: avail = G.boxes.filter(b => b.state==='shelf' && !b.orderedBy) — для спавна клиента.</summary>
        public static Box FindAvailableOrderableBox()
        {
            var available = new List<Box>();
            foreach (var box in AllBoxes)
                if (box.state == BoxState.OnShelf && box.OrderedBy == null)
                    available.Add(box);

            if (available.Count == 0) return null;
            return available[Random.Range(0, available.Count)];
        }

        public void PlaceOnDock()
        {
            state = BoxState.Dock;
            assignedCell = null;
        }

        public void PickUp(ShelfCell targetCell)
        {
            state = BoxState.Carried;
            assignedCell = targetCell;
        }

        public void PlaceOnShelf(ShelfCell cell)
        {
            state = BoxState.OnShelf;
            assignedCell = cell;
            transform.position = cell.transform.position;
        }

        public void HandOver()
        {
            state = BoxState.HandedOver;
            if (OrderedBy != null) OrderedBy.OrderBox = null;
        }

        public string GetPrompt(PlayerInteraction player)
        {
            if (player.CarriedBox != null) return string.Empty;

            switch (state)
            {
                case BoxState.Dock:
                    return "Взять коробку";
                case BoxState.OnShelf:
                    // MVP: забрать с полки можно только если её ждёт клиент у окна.
                    if (OrderedBy != null && OrderedBy.State == CustomerState.AtWindow)
                        return $"Взять заказ ({assignedCell?.Label})";
                    return string.Empty;
                default:
                    return string.Empty;
            }
        }

        public void Interact(PlayerInteraction player)
        {
            if (state == BoxState.Dock)
            {
                var cell = ShelfCell.FindFreeCell();
                if (cell == null)
                {
                    Debug.Log("Стеллажи забиты! Освободи ячейки.");
                    return;
                }
                cell.Reserve();
                PickUp(cell);
                player.PickUpBox(this);
            }
            else if (state == BoxState.OnShelf && OrderedBy != null && OrderedBy.State == CustomerState.AtWindow)
            {
                assignedCell.Release();
                PickUp(null);
                player.PickUpBox(this);
            }
        }
    }
}
