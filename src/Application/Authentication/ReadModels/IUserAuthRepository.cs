using Ratatosk.Application.Authentication.ReadModels;
using Ratatosk.Application.Shared;

public interface IUserAuthRepository
{
    Task<Pagination<UserAuth>> GetAllAsync(CancellationToken cancellationToken);
    Task<Pagination<UserAuth>> GetAllByRole(
        string role,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default
    );
    Task<UserAuth?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task SaveAsync(UserAuth userAuth, CancellationToken cancellationToken);
    Task DeleteAsync(string email, CancellationToken cancellationToken);
}
