using Ratatosk.Application.Authentication.ReadModels;

public interface IUserAuthRepository
{
    Task<IEnumerable<UserAuth>> GetAllAsync(CancellationToken cancellationToken);
    Task<IEnumerable<UserAuth>> GetAllByRole(string role, CancellationToken cancellationToken);
    Task<UserAuth?> GetByEmailAsync(string email, CancellationToken cancellationToken);
}
