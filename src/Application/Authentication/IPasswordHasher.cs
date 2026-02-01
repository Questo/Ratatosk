using Ratatosk.Domain.Identity;

namespace Ratatosk.Application.Authentication;

public interface IPasswordHasher
{
    PasswordHash Hash(Password password);
    bool Verify(Password password, PasswordHash hash);
}
