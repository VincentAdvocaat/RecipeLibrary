using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.Demo;

internal sealed class DemoCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner, IDemoOperationRunner? runner) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default) =>
        runner is null ? inner.HandleAsync(command, ct)
            : runner.RunAsync(command, () => inner.HandleAsync(command, ct), ct);
}

internal sealed class DemoQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner, IDemoOperationRunner? runner) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default) =>
        runner is null ? inner.HandleAsync(query, ct)
            : runner.RunAsync(query, () => inner.HandleAsync(query, ct), ct);
}
