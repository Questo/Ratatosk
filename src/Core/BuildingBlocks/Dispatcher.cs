using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Core.BuildingBlocks;

public class Dispatcher(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<Result<TResult>> DispatchQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var handler = _serviceProvider.GetRequiredService<IHandler<IQuery<TResult>, TResult>>();
        return await handler.HandleAsync(query, cancellationToken);
    }

    public async Task<Result> DispatchCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        var handler = _serviceProvider.GetRequiredService<IHandler<TCommand>>();
        return await handler.HandleAsync(command, cancellationToken);
    }
}