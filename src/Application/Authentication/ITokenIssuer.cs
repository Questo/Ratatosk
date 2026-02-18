using Ratatosk.Core.Primitives;

namespace Ratatosk.Application.Authentication;

public interface ITokenIssuer
{
    Result<string> IssueToken(string email, string role, string hash);
}
