namespace Gazon.Interaction
{
    /// <summary>Действие, требующее удержания E (в MVP — только стол возвратов, 1.3 сек).</summary>
    public interface IHoldInteractable : IInteractable
    {
        float HoldDuration { get; }
    }
}
