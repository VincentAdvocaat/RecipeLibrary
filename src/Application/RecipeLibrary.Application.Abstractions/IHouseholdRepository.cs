using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.Abstractions;

public interface IHouseholdRepository
{
    Task<HouseholdSummary?> GetAsync(string userId, CancellationToken ct);
    Task<HouseholdDetails?> GetDetailsAsync(string userId, CancellationToken ct);
    Task<bool> IsMemberAsync(string userId, Guid householdId, CancellationToken ct);
    Task<bool> CreateAsync(string userId, string name, CancellationToken ct);
    Task<bool> CreateInvitationAsync(string userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct);
    Task<JoinHouseholdResult> JoinAsync(string userId, string tokenHash, DateTimeOffset now, CancellationToken ct);
    Task<bool> RemoveMemberAsync(string userId, string memberUserId, CancellationToken ct);
    Task<bool> TransferManagementAsync(string userId, string memberUserId, CancellationToken ct);
    Task<bool> LeaveAsync(string userId, CancellationToken ct);
}
