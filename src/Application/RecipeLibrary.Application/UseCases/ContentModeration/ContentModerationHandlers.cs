using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.ContentModeration;

public sealed class GetModerationQueueQueryHandler(IContentModerationStore store)
    : IQueryHandler<GetModerationQueueQuery, ModerationQueueResult>
{
    public async Task<ModerationQueueResult> HandleAsync(GetModerationQueueQuery query, CancellationToken ct = default)
    {
        var needsReview = await store.ListNeedsReviewAsync(ct);
        var reports = await store.ListOpenReportsAsync(ct);
        return new ModerationQueueResult(needsReview, reports);
    }
}

public sealed class SetRecipeModerationDecisionCommandHandler(
    IContentModerationStore store,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetRecipeModerationDecisionCommand, SetRecipeModerationDecisionResult>
{
    public async Task<SetRecipeModerationDecisionResult> HandleAsync(
        SetRecipeModerationDecisionCommand command,
        CancellationToken ct = default)
    {
        _ = currentUser.UserId ?? throw new UnauthorizedAccessException("Authentication is required.");

        if (!Enum.TryParse<ModerationStatus>(command.Decision, ignoreCase: true, out var decision)
            || decision is not (ModerationStatus.Approved or ModerationStatus.Rejected))
        {
            throw new ArgumentException("Decision must be Approved or Rejected.", nameof(command));
        }

        var recipe = await store.GetRecipeForAdminAsync(command.RecipeId, ct)
            ?? throw new InvalidOperationException($"Recipe '{command.RecipeId}' was not found.");

        await store.SetRecipeModerationStatusAsync(
            recipe.Id,
            decision,
            summary: $"manual:{decision}",
            ct);

        if (command.RelatedReportId is { } reportId)
        {
            await store.MarkReportHandledAsync(reportId, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new SetRecipeModerationDecisionResult(recipe.Id, decision.ToString());
    }
}

public sealed class ReportRecipeContentCommandHandler(
    IContentModerationStore store,
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReportRecipeContentCommand, ReportRecipeContentResult>
{
    public async Task<ReportRecipeContentResult> HandleAsync(
        ReportRecipeContentCommand command,
        CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        // Allow reporting any recipe the caller can see in their library (private ownership today).
        var recipe = await recipeRepository.GetByIdAsync(userId, command.RecipeId, ct)
            ?? throw new InvalidOperationException($"Recipe '{command.RecipeId}' was not found.");

        var reportId = Guid.NewGuid();
        await store.AddReportAsync(
            new Domain.Entities.ContentReport
            {
                Id = reportId,
                RecipeId = recipe.Id,
                ReporterUserId = userId,
                Reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                Handled = false,
            },
            ct);

        // Flag for admin attention when not already rejected.
        if (recipe.ModerationStatus is not ModerationStatus.Rejected and not ModerationStatus.NeedsReview)
        {
            await store.SetRecipeModerationStatusAsync(
                recipe.Id,
                ModerationStatus.NeedsReview,
                summary: "user-report",
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new ReportRecipeContentResult(reportId);
    }
}
