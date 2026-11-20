using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.UseCases.Pantry;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class HouseholdTransactionTests
{
    [Theory]
    [InlineData("get")]
    [InlineData("upsert")]
    [InlineData("remove")]
    public async Task Pantry_ChecksMembershipAndOperatesInsideSerializableBoundary(string operation)
    {
        var unit = new HouseholdTransactionUnitOfWork();
        var households = new HouseholdMembershipStub(() =>
        {
            Assert.True(unit.Active);
            return true;
        });
        var pantry = new RecordingPantryRepository();
        var shopping = new RecordingShoppingListRepository();
        var user = new FixedCurrentUser("member");
        var id = Guid.NewGuid();
        switch (operation)
        {
            case "get":
                await new GetPantryItemsQueryHandler(pantry, shopping, user, households, unit)
                    .HandleAsync(new GetPantryItemsQuery { HouseholdId = id });
                break;
            case "upsert":
                await new UpsertPantryItemCommandHandler(pantry, shopping, user,
                    new PantryIngredientMerger(new IngredientTextNormalizer()), unit, households)
                    .HandleAsync(new UpsertPantryItemCommand { HouseholdId = id, DisplayName = "Salt" });
                break;
            case "remove":
                await new RemovePantryItemCommandHandler(pantry, shopping, user, unit, households)
                    .HandleAsync(new RemovePantryItemCommand { HouseholdId = id, ItemId = Guid.NewGuid() });
                break;
        }
        Assert.True(unit.Committed);
        Assert.Equal(1, households.Checks);
    }
}

internal sealed class HouseholdTransactionUnitOfWork : IUnitOfWork
{
    public bool Active { get; private set; }
    public bool Committed { get; private set; }
    public Task SaveChangesAsync(CancellationToken ct = default) => throw new InvalidOperationException("Save must belong to transaction.");
    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) =>
        throw new InvalidOperationException("Serializable isolation required.");
    public async Task ExecuteInSerializableTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        Active = true;
        try
        {
            await action(ct);
            Committed = true;
        }
        finally { Active = false; }
    }
}

internal sealed class HouseholdMembershipStub(Func<bool> check) : IHouseholdRepository
{
    public int Checks { get; private set; }
    public Task<bool> IsMemberAsync(string userId, Guid householdId, CancellationToken ct)
    {
        Checks++;
        return Task.FromResult(check());
    }
    public Task<HouseholdSummary?> GetAsync(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<HouseholdDetails?> GetDetailsAsync(string userId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> CreateAsync(string userId, string name, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> CreateInvitationAsync(string userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct) => throw new NotSupportedException();
    public Task<JoinHouseholdResult> JoinAsync(string userId, string tokenHash, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> RemoveMemberAsync(string userId, string memberUserId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> TransferManagementAsync(string userId, string memberUserId, CancellationToken ct) => throw new NotSupportedException();
    public Task<bool> LeaveAsync(string userId, CancellationToken ct) => throw new NotSupportedException();
}
