namespace Ratatosk.Application.Authentication;

public interface IUserRepository
{
    Task<User> GetUserAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetUsersAsync(UserRole? role = null, CancellationToken cancellationToken = default);
    Task SaveUserAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(User user, CancellationToken cancellationToken = default);
}