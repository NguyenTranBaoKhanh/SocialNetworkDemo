namespace Application.Auth;

public record RegisterRequest(string Username, string Email, string Password, string DisplayName);

public record LoginRequest(string UsernameOrEmail, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record AuthResponse(
    string Token,                    // access token (JWT, sống ngắn)
    DateTimeOffset ExpiresAt,        // hạn của access token
    string RefreshToken,             // refresh token (sống dài, dùng xin access token mới)
    long UserId,
    string Username,
    string DisplayName);
