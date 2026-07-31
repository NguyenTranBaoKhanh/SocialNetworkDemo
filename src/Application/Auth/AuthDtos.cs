namespace Application.Auth;

public record RegisterRequest(string Username, string Email, string Password, string DisplayName);

public record LoginRequest(string UsernameOrEmail, string Password);

public record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    long UserId,
    string Username,
    string DisplayName);
