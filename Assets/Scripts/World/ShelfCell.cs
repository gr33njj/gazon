using System.Collections.Generic;
using Gazon.Core;
using Gazon.Interaction;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>Одна ячейка стеллажа (в MVP — 3 стеллажа x 8 ячеек = 24 штуки, метки вида "А3").</summary>
    // BoxCollider (не abstract Collider — иначе Unity не сможет добавить его автоматически) нужен,
    // чтобы Physics.Raycast из PlayerInteraction видел эту ячейку.
    [RequireComponent(typeof(BoxCollider))]
    public class ShelfCell : MonoBehaviour, IInteractable
    {
        private static readonly List<ShelfCell> AllCells = new List<ShelfCell>();

        [SerializeField] private string label = "A1";
        [SerializeField] private Box currentBox;
        private bool reserved;

        public string Label => label;
        public Box CurrentBox => currentBox;

        private void OnEnable() => AllCells.Add(this);
        private void OnDisable() => AllCells.Remove(this);

        /// <summary>MVP: freeCell() — случайная свободная и незарезервированная ячейка.</summary>
        public static ShelfCell FindFreeCell()
        {
            var free = new List<ShelfCell>();
            foreach (var cell in AllCells)
                if (cell.currentBox == null && !cell.reserved)
                    free.Add(cell);

            if (free.Count == 0) return null;
            return free[Random.Range(0, free.Count)];
        }

        public void Reserve()
        {
            reserved = true;
        }

        public void Release()
        {
            reserved = false;
            currentBox = null;
        }

        public string GetPrompt(PlayerInteraction player)
        {
            var box = player.CarriedBox;
            if (box == null) return string.Empty;
            if (box.AssignedCell == this) return $"Положить в ячейку {label}";
            return string.Empty;
        }

        public void Interact(PlayerInteraction player)
        {
            var box = player.CarriedBox;
            if (box == null || box.AssignedCell != this) return;

            currentBox = box;
            reserved = false;
            box.PlaceOnShelf(this);
            player.DropCarriedBox();

            GameManager.Instance.AddMoney(10f);
        }
    }
}
