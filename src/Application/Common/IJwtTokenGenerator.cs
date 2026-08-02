using Domain.Entities;

namespace Application.Common;

/// <summary>Token thô (gửi cho client) + hash (lưu DB) + hạn.</summary>
public record RefreshTokenResult(string RawToken, string TokenHash, DateTimeOffset ExpiresAt);

public interface IJwtTokenGenerator
{
    /// <summary>Sinh JWT access token (sống ngắn); trả về (token, thời điểm hết hạn).</summary>
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user);

    /// <summary>Sinh refresh token ngẫu nhiên (sống dài) kèm hash để lưu DB.</summary>
    RefreshTokenResult GenerateRefreshToken();

    /// <summary>Hash một refresh token thô để tra cứu trong DB (khi refresh/logout).</summary>
    string HashRefreshToken(string rawToken);
}
