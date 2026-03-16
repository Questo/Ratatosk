using Ratatosk.Application.Authentication.Models;

namespace Ratatosk.Application.Authentication;

public interface IRefreshTokenRepository
{
    Task SaveAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}
