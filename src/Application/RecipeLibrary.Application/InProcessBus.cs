using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application;

internal sealed class InProcessBus(IServiceProvider serviceProvider) : ICommandBus, IQueryBus
{
    public Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResult>
    {
        ArgumentNullException.ThrowIfNull(command);
        var handler = (ICommandHandler<TCommand, TResult>?)serviceProvider.GetService(typeof(ICommandHandler<TCommand, TResult>));
        if (handler is null)
        {
            throw new InvalidOperationException($"No command handler registered for {typeof(TCommand).FullName} -> {typeof(TResult).FullName}.");
        }

        return handler.HandleAsync(command, ct);
    }

    public Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>
    {
        ArgumentNullException.ThrowIfNull(query);
        var handler = (IQueryHandler<TQuery, TResult>?)serviceProvider.GetService(typeof(IQueryHandler<TQuery, TResult>));
        if (handler is null)
        {
            throw new InvalidOperationException($"No query handler registered for {typeof(TQuery).FullName} -> {typeof(TResult).FullName}.");
        }

        return handler.HandleAsync(query, ct);
    }
}

