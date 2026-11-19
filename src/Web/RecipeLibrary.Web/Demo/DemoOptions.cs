namespace RecipeLibrary.Web.Demo;

public sealed class DemoOptions
{
    public bool Enabled { get; set; }
    public int LifetimeMinutes { get; set; } = 60;
    public int CleanupIntervalMinutes { get; set; } = 5;
    public int StartsPerIpPerHour { get; set; } = 5;
    public int MaxActiveSessions { get; set; } = 200;
    public int MaxResets { get; set; } = 5;
    public int MaxRecipes { get; set; } = 20;
    public int MaxLists { get; set; } = 5;
    public int MaxItemsPerList { get; set; } = 100;
    public int MaxPantryItems { get; set; } = 100;
    public int MaxSaveAttempts { get; set; } = 30;
}
