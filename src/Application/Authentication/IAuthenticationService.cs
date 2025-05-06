using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Authentication;

public interface IAuthenticationService
{
    Task<Result<string>> LoginAsync(string username, string password, CancellationToken cancellationToken);
    //Task<Result<string>> RegisterAsync(string username, string password, CancellationToken cancellationToken);
}