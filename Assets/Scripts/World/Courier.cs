using Gazon.Core;
using Gazon.Interaction;
using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>
    /// Курьер с претензией по хрупкому товару. MVP: 55% — извинился, компенсация +100₽;
    /// 45% — штраф 150₽ и −0.1★. Если не подойти вовремя — сам уходит (см. CourierSpawner).
    /// </summary>
    public class Courier : MonoBehaviour, IInteractable
    {
        public string GetPrompt(PlayerInteraction player)
        {
            return "Вызвать курьера на стрелку";
        }

        public void Interact(PlayerInteraction player)
        {
            if (Random.value < 0.55f)
            {
                GameManager.Instance.Earn(100f);
                GameManager.Instance.Toast("🤝 «Извини, брат, спешил». Компенсация +100 ₽", "good");
            }
            else
            {
                GameManager.Instance.Fine(150f, "У курьера кенты в Яндекс.Доставке. Стрелка не задалась");
                GameManager.Instance.AddRating(-0.1f);
            }

            CourierSpawner.Instance?.ResolveCourier();
        }
    }
}
