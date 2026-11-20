namespace RecipeLibrary.Domain.Entities;

public sealed class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ManagerUserId { get; set; } = string.Empty;
}

public sealed class HouseholdMember
{
    public string UserId { get; set; } = string.Empty;
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
}

public sealed class HouseholdInvitation
{
    public string TokenHash { get; set; } = string.Empty;
    public Guid HouseholdId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
