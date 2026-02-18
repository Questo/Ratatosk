namespace Ratatosk.Application.Authentication.ReadModels;

public sealed record UserAuth(string Email, string Role, string Hash);
