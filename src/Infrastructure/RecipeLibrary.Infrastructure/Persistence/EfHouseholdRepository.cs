using System.Data;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfHouseholdRepository(RecipeDbContext dbContext) : IHouseholdRepository
{
    public Task<HouseholdSummary?> GetAsync(string userId, CancellationToken ct) =>
        dbContext.HouseholdMembers
            .Where(member => member.UserId == userId)
            .Select(member => new HouseholdSummary(member.HouseholdId, member.Household.Name,
                member.Household.ManagerUserId == userId))
            .SingleOrDefaultAsync(ct);

    public async Task<HouseholdDetails?> GetDetailsAsync(string userId, CancellationToken ct)
    {
        var members = await (
            from caller in dbContext.HouseholdMembers
            where caller.UserId == userId
            join member in dbContext.HouseholdMembers on caller.HouseholdId equals member.HouseholdId
            join account in dbContext.Users on member.UserId equals account.Id
            orderby account.UserName
            select new
            {
                member.HouseholdId,
                member.Household.Name,
                member.Household.ManagerUserId,
                member.UserId,
                AccountName = account.UserName ?? account.Id,
            }).ToListAsync(ct);

        if (members.Count == 0)
        {
            return null;
        }

        var first = members[0];
        var summary = new HouseholdSummary(first.HouseholdId, first.Name, first.ManagerUserId == userId);
        return new HouseholdDetails(summary,
            members.Select(member => new HouseholdMemberDto(member.UserId, member.AccountName,
                member.ManagerUserId == member.UserId, member.UserId == userId)).ToList());
    }

    public Task<bool> IsMemberAsync(string userId, Guid householdId, CancellationToken ct) =>
        dbContext.HouseholdMembers.AnyAsync(member => member.UserId == userId && member.HouseholdId == householdId, ct);

    public Task<bool> CreateAsync(string userId, string name, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            if (await dbContext.HouseholdMembers.AnyAsync(member => member.UserId == userId, ct))
            {
                return false;
            }

            var household = new Household { Id = Guid.NewGuid(), Name = name, ManagerUserId = userId };
            dbContext.HouseholdMembers.Add(new HouseholdMember
            {
                UserId = userId,
                HouseholdId = household.Id,
                Household = household,
            });
            return true;
        }, ct);

    public Task<bool> CreateInvitationAsync(string userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            var householdId = await ManagedHouseholds(userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (householdId is null)
            {
                return false;
            }

            dbContext.HouseholdInvitations.Add(new HouseholdInvitation
            {
                HouseholdId = householdId.Value,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
            });
            return true;
        }, ct);

    public Task<JoinHouseholdResult> JoinAsync(string userId, string tokenHash, DateTimeOffset now, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            if (await dbContext.HouseholdMembers.AnyAsync(member => member.UserId == userId, ct))
            {
                return JoinHouseholdResult.AlreadyInHousehold;
            }

            var validInvitation = dbContext.HouseholdInvitations.Where(invite =>
                invite.TokenHash == tokenHash && invite.ExpiresAt > now);
            var householdId = await validInvitation.Select(invite => (Guid?)invite.HouseholdId).SingleOrDefaultAsync(ct);
            if (householdId is null || await validInvitation.ExecuteDeleteAsync(ct) != 1)
            {
                return JoinHouseholdResult.InvalidInvitation;
            }

            dbContext.HouseholdMembers.Add(new HouseholdMember { UserId = userId, HouseholdId = householdId.Value });
            return JoinHouseholdResult.Joined;
        }, ct);

    public Task<bool> RemoveMemberAsync(string userId, string memberUserId, CancellationToken ct) =>
        InTransactionAsync(async () => await dbContext.HouseholdMembers
            .Where(member => member.UserId == memberUserId && member.UserId != userId
                && ManagedHouseholds(userId).Any(household => household.Id == member.HouseholdId))
            .ExecuteDeleteAsync(ct) == 1, ct);

    public Task<bool> TransferManagementAsync(string userId, string memberUserId, CancellationToken ct) =>
        InTransactionAsync(async () => await ManagedHouseholds(userId)
            .Where(household => memberUserId != userId && dbContext.HouseholdMembers.Any(member =>
                member.HouseholdId == household.Id && member.UserId == memberUserId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(household => household.ManagerUserId, memberUserId), ct) == 1, ct);

    public Task<bool> LeaveAsync(string userId, CancellationToken ct) =>
        InTransactionAsync(async () => await dbContext.HouseholdMembers
            .Where(member => member.UserId == userId && member.Household.ManagerUserId != userId)
            .ExecuteDeleteAsync(ct) == 1, ct);

    private IQueryable<Household> ManagedHouseholds(string userId) =>
        dbContext.Households.Where(household => household.ManagerUserId == userId
            && dbContext.HouseholdMembers.Any(member => member.UserId == userId && member.HouseholdId == household.Id));

    private Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var result = await action();
                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                dbContext.ChangeTracker.Clear();
                return result;
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        });
}
