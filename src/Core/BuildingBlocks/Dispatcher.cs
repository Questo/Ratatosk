using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Core.Abstractions;
using Ratatosk.Core.Primitives;

namespace Ratatosk.Core.BuildingBlocks;

public class Dispatcher(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<Result<TResult>> DispatchQueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        var handler = _serviceProvider.GetRequiredService(handlerType);

        var method = handlerType.GetMethod(nameof(IHandler<IQuery<TResult>, TResult>.HandleAsync))!;
        var task = (Task<Result<TResult>>)method.Invoke(handler, [query, cancellationToken])!;
        return await task;
    }

    public async Task<Result> DispatchCommandAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        var handler = _serviceProvider.GetRequiredService<IHandler<TCommand>>();
        return await handler.HandleAsync(command, cancellationToken);
    }
}