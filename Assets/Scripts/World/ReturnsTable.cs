using Gazon.Core;
using Gazon.Interaction;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>Оранжевый стол возвратов. MVP: удержание E 1.3 сек, +15₽.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ReturnsTable : MonoBehaviour, IHoldInteractable
    {
        public float HoldDuration => 1.3f;

        public string GetPrompt(PlayerInteraction player)
        {
            var box = player.CarriedBox;
            if (box == null || !box.IsReturn) return string.Empty;
            return "Обработать возврат (держи E)";
        }

        public void Interact(PlayerInteraction player)
        {
            var box = player.CarriedBox;
            if (box == null || !box.IsReturn) return;

            player.DropCarriedBox(destroyBox: true);
            GameManager.Instance.Earn(15f);
            GameManager.Instance.RecordReturnProcessed();
            GameManager.Instance.Toast("↩️ Возврат обработан. +15 ₽");
        }
    }
}
