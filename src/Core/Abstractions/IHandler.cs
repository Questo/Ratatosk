using Ratatosk.Core.Primitives;

namespace Ratatosk.Core.Abstractions;

public interface IHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

public interface IHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
