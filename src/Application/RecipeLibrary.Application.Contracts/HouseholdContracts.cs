namespace RecipeLibrary.Application.Contracts;

public sealed record HouseholdSummary(Guid Id, string Name, bool CanManage);
public sealed record HouseholdMemberDto(string UserId, string Name, bool IsManager, bool IsYou);
public sealed record HouseholdDetails(HouseholdSummary Household, IReadOnlyList<HouseholdMemberDto> Members);

public sealed record GetHouseholdQuery : IQuery<HouseholdSummary?>;
public sealed record GetHouseholdDetailsQuery : IQuery<HouseholdDetails?>;
public sealed record CreateHouseholdCommand(string Name) : ICommand<bool>;
public sealed record CreateHouseholdInvitationCommand : ICommand<string?>;
public sealed record JoinHouseholdCommand(string Token) : ICommand<JoinHouseholdResult>;
public sealed record RemoveHouseholdMemberCommand(string UserId) : ICommand<bool>;
public sealed record TransferHouseholdManagementCommand(string UserId) : ICommand<bool>;
public sealed record LeaveHouseholdCommand : ICommand<bool>;

public enum JoinHouseholdResult
{
    Joined,
    InvalidInvitation,
    AlreadyInHousehold,
}

public sealed record ShareRecipesWithHouseholdCommand(Guid HouseholdId, IReadOnlyList<Guid> RecipeIds)
    : ICommand<ShareRecipesWithHouseholdResult>;

public sealed record ShareRecipesWithHouseholdResult(int Shared, int AlreadyShared, int Unavailable);
