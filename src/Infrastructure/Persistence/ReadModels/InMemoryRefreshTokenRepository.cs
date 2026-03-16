using System.Collections.Concurrent;
using Ratatosk.Application.Authentication;
using Ratatosk.Application.Authentication.Models;

namespace Ratatosk.Infrastructure.Persistence.ReadModels;

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ConcurrentDictionary<string, RefreshToken> _tokens = new();

    public Task SaveAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _tokens[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _tokens.TryGetValue(token, out var result);
        return Task.FromResult(result);
    }

    public Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        _tokens.TryRemove(token, out _);
        return Task.CompletedTask;
    }
}
