using Ratatosk.Core.Primitives;
using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication;

public interface ITokenIssuer
{
    Result<string> IssueToken(User user);
}
