using Ratatosk.Infrastructure.Persistence;

namespace Ratatosk.API.Middleware;

/// <summary>
/// Ends the request's transaction before the response is written: commits when the endpoint
/// succeeded (status &lt; 400), rolls back otherwise. Aggregate commits happen earlier, inside
/// IAggregateRepository.CommitAsync; this covers the request's other writes (e.g. refresh tokens).
/// </summary>
public sealed class UnitOfWorkEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        // The request scope's instance: the same one the endpoint's handlers wrote through.
        var uow = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

        object? result;
        try
        {
            result = await next(context);
        }
        catch
        {
            uow.Rollback();
            throw;
        }

        var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
        if (statusCode < 400)
            uow.Commit();
        else
            uow.Rollback();

        return result;
    }
}
