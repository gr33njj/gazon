namespace Gazon.Interaction
{
    /// <summary>Общий контракт для всего, на что игрок может навести прицел и нажать E.</summary>
    public interface IInteractable
    {
        /// <summary>Текст подсказки над прицелом, например "Взять коробку". Пустая строка — действие недоступно.</summary>
        string GetPrompt(Player.PlayerInteraction player);

        /// <summary>Выполнить действие. Вызывается только если GetPrompt вернул непустую строку.</summary>
        void Interact(Player.PlayerInteraction player);
    }
}
