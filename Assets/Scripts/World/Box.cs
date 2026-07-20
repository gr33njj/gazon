using System.Collections.Generic;
using Gazon.Core;
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
        HandedOver,
        CounterReturn
    }

    /// <summary>
    /// Коробка. Жизненный цикл: Dock -> Carried -> OnShelf -> Carried -> HandedOver, либо
    /// CounterReturn -> Carried -> (обработана на столе возвратов). fragile/dented — см. DockSpawner.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))] // нужен коллайдер, чтобы Physics.Raycast из PlayerInteraction её видел
    public class Box : MonoBehaviour, IInteractable
    {
        private static readonly List<Box> AllBoxes = new List<Box>();

        [SerializeField] private BoxState state = BoxState.Dock;
        [SerializeField] private ShelfCell assignedCell;
        [SerializeField] private bool isFragile;
        [SerializeField] private bool isDented;
        [SerializeField] private bool isReturn;

        private float dockTimer;
        private bool dockFined;

        public BoxState State => state;
        public ShelfCell AssignedCell => assignedCell;
        public Customer OrderedBy { get; set; }
        public bool IsFragile => isFragile;
        public bool IsDented => isDented;
        public bool IsReturn => isReturn;

        private void OnEnable() => AllBoxes.Add(this);
        private void OnDisable() => AllBoxes.Remove(this);

        private void Update()
        {
            // MVP: dockT>35 && !fined -> штраф 50₽ "Просрочена приёмка" (один раз за коробку).
            if (state != BoxState.Dock) return;
            dockTimer += Time.deltaTime;
            if (dockTimer > 35f && !dockFined)
            {
                dockFined = true;
                GameManager.Instance.Fine(50f, "Просрочена приёмка");
            }
        }

        /// <summary>Для HUD: сколько коробок сейчас на приёмке (не разложено по полкам).</summary>
        public static int CountOnDock()
        {
            int n = 0;
            foreach (var box in AllBoxes)
                if (box.state == BoxState.Dock) n++;
            return n;
        }

        /// <summary>MVP: avail = G.boxes.filter(b => b.state==='shelf' && !b.orderedBy && !b.isReturn).</summary>
        public static Box FindAvailableOrderableBox()
        {
            var available = new List<Box>();
            foreach (var box in AllBoxes)
                if (box.state == BoxState.OnShelf && box.OrderedBy == null && !box.isReturn)
                    available.Add(box);

            if (available.Count == 0) return null;
            return available[Random.Range(0, available.Count)];
        }

        public void PlaceOnDock()
        {
            state = BoxState.Dock;
            assignedCell = null;
            dockTimer = 0f;
            dockFined = false;
        }

        /// <summary>Настраивает партию с докстанции — см. DockSpawner (30% хрупкое, 40% из них помятое).</summary>
        public void ConfigureDelivery(bool fragile, bool dented)
        {
            isFragile = fragile;
            isDented = dented;
        }

        /// <summary>Коробка появляется на столе возвратов (после примерочной) — не идёт на полку.</summary>
        public void PlaceAsCounterReturn()
        {
            isReturn = true;
            state = BoxState.CounterReturn;
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
                    return isFragile ? "Взять коробку (хрупкое!)" : "Взять коробку";
                case BoxState.OnShelf:
                    // MVP: забрать с полки можно только если её ждёт клиент у окна.
                    if (OrderedBy != null && OrderedBy.State == CustomerState.AtWindow)
                        return $"Взять заказ ({assignedCell?.Label})";
                    return string.Empty;
                case BoxState.CounterReturn:
                    return "Взять возврат";
                default:
                    return string.Empty;
            }
        }

        public void Interact(PlayerInteraction player)
        {
            switch (state)
            {
                case BoxState.Dock:
                {
                    var cell = ShelfCell.FindFreeCell();
                    if (cell == null)
                    {
                        GameManager.Instance.Toast("Стеллажи забиты! Освободи ячейки.", "warn");
                        return;
                    }
                    cell.Reserve();
                    PickUp(cell);
                    player.PickUpBox(this);
                    break;
                }
                case BoxState.OnShelf:
                    if (OrderedBy != null && OrderedBy.State == CustomerState.AtWindow)
                    {
                        assignedCell.Release();
                        PickUp(null);
                        player.PickUpBox(this);
                    }
                    break;
                case BoxState.CounterReturn:
                    PickUp(null);
                    player.PickUpBox(this);
                    break;
            }
        }
    }
}
