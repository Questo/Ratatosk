namespace Ratatosk.Domain.Identity;

public interface IUserDomainService
{
    Task<bool> IsEmailUnique(string email, CancellationToken cancellationToken = default);
}
