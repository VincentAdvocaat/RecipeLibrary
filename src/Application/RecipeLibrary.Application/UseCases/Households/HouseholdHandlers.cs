using System.Security.Cryptography;
using System.Text;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Households;

public sealed class HouseholdHandlers(IHouseholdRepository repository, ICurrentUser currentUser)
    : IQueryHandler<GetHouseholdQuery, HouseholdSummary?>,
      IQueryHandler<GetHouseholdDetailsQuery, HouseholdDetails?>,
      ICommandHandler<CreateHouseholdCommand, bool>,
      ICommandHandler<CreateHouseholdInvitationCommand, string?>,
      ICommandHandler<JoinHouseholdCommand, JoinHouseholdResult>,
      ICommandHandler<RemoveHouseholdMemberCommand, bool>,
      ICommandHandler<TransferHouseholdManagementCommand, bool>,
      ICommandHandler<LeaveHouseholdCommand, bool>
{
    private string UserId => currentUser.UserId
        ?? throw new UnauthorizedAccessException("Authentication is required.");

    public Task<HouseholdSummary?> HandleAsync(GetHouseholdQuery query, CancellationToken ct = default) =>
        repository.GetAsync(UserId, ct);

    public Task<HouseholdDetails?> HandleAsync(GetHouseholdDetailsQuery query, CancellationToken ct = default) =>
        repository.GetDetailsAsync(UserId, ct);

    public Task<bool> HandleAsync(CreateHouseholdCommand command, CancellationToken ct = default)
    {
        var name = command.Name.Trim();
        if (name.Length is < 1 or > 100)
        {
            throw new ArgumentException("Household names must contain 1 to 100 characters.");
        }

        return repository.CreateAsync(UserId, name, ct);
    }

    public async Task<string?> HandleAsync(CreateHouseholdInvitationCommand command, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return await repository.CreateInvitationAsync(UserId, HashToken(token), DateTimeOffset.UtcNow.AddDays(7), ct)
            ? token : null;
    }

    public Task<JoinHouseholdResult> HandleAsync(JoinHouseholdCommand command, CancellationToken ct = default)
    {
        var userId = UserId;
        return command.Token.Length == 64 && command.Token.All(Uri.IsHexDigit)
            ? repository.JoinAsync(userId, HashToken(command.Token.ToUpperInvariant()), DateTimeOffset.UtcNow, ct)
            : Task.FromResult(JoinHouseholdResult.InvalidInvitation);
    }

    public Task<bool> HandleAsync(RemoveHouseholdMemberCommand command, CancellationToken ct = default) =>
        repository.RemoveMemberAsync(UserId, command.UserId, ct);

    public Task<bool> HandleAsync(TransferHouseholdManagementCommand command, CancellationToken ct = default) =>
        repository.TransferManagementAsync(UserId, command.UserId, ct);

    public Task<bool> HandleAsync(LeaveHouseholdCommand command, CancellationToken ct = default) =>
        repository.LeaveAsync(UserId, ct);

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
