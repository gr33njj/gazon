using Gazon.Interaction;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>Служебный выход на перекур. Недоступен во время кулдауна (см. SmokeBreakController).</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SmokeDoor : MonoBehaviour, IInteractable
    {
        public string GetPrompt(PlayerInteraction player)
        {
            var controller = SmokeBreakController.Instance;
            if (controller == null) return string.Empty;
            if (!controller.CanStart)
                return $"Перекур недоступен ({Mathf.CeilToInt(controller.CooldownRemaining)}с)";
            return "Закрыть ПВЗ и выйти на перекур";
        }

        public void Interact(PlayerInteraction player)
        {
            SmokeBreakController.Instance?.StartBreak();
        }
    }
}
