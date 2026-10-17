using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Authentication;

public interface ITokenIssuer
{
    Result<string> IssueToken(Guid userId, string email, string role);
}
