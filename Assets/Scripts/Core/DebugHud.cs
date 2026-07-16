using UnityEngine;

namespace Gazon.Core
{
    /// <summary>
    /// Временный оверлей на OnGUI (без Canvas/TMP) — чтобы увидеть, что цикл работает, до того как
    /// появится настоящий UI. Повесить на любой объект сцены, ничего настраивать не нужно.
    /// Убрать/заменить нормальным UI, когда дойдёт очередь — см. Docs/Decisions.md.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        private Player.PlayerInteraction interaction;
        private string prompt = "";

        private void Start()
        {
            interaction = FindFirstObjectByType<Player.PlayerInteraction>();
            if (interaction != null)
                interaction.OnPromptChanged.AddListener(p => prompt = p);
        }

        private void OnGUI()
        {
            if (GameManager.Instance == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(10, 10, 700, 30),
                $"День {GameManager.Instance.Day}  |  {GameManager.Instance.ShiftTimeRemaining:0} сек  |  " +
                $"{GameManager.Instance.Money:0} ₽  |  {GameManager.Instance.Rating:0.0} ★", style);

            if (!string.IsNullOrEmpty(prompt))
                GUI.Label(new Rect(10, 40, 700, 30), $"[E] {prompt}", style);
        }
    }
}
