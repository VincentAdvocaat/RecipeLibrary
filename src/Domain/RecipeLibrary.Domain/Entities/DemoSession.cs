namespace RecipeLibrary.Domain.Entities;

public sealed class DemoSession
{
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int SeedVersion { get; set; } = 1;
    public int Revision { get; set; } = 1;
    public int ResetCount { get; set; }
    public int SaveAttempts { get; set; }
}
