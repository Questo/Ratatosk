namespace Ratatosk.API.Auth;

public record LoginRequest(string Email, string Password);

public record SignUpRequest(string Email, string Password);
