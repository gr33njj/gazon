namespace Gazon.Customers
{
    /// <summary>Имена архетипов соответствуют customer_archetypes.name в Database/schema.sql.</summary>
    public enum CustomerArchetype
    {
        Normal,
        Babka,
        Shopaholic
    }

    public static class CustomerArchetypeExtensions
    {
        public static string ToDbName(this CustomerArchetype archetype) => archetype switch
        {
            CustomerArchetype.Normal => "normal",
            CustomerArchetype.Babka => "babka",
            CustomerArchetype.Shopaholic => "shopaholic",
            _ => "normal"
        };
    }
}
