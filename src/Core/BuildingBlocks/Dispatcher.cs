using Microsoft.Extensions.DependencyInjection;
using Ratatosk.Core.Abstractions;

namespace Ratatosk.Core.BuildingBlocks;

public class Dispatcher(IServiceProvider serviceProvider)
{
    public async Task<TResponse> DispatchAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        var method = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync))!;
        var task = (Task<TResponse>)method.Invoke(handler, [request, cancellationToken])!;
        return await task;
    }
}