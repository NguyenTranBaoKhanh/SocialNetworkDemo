using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Refresh token lưu ở DB (nên THU HỒI được — khác với access token JWT stateless).
/// Chỉ lưu HASH của token, không lưu plaintext: rò rỉ DB không lộ token dùng được.
/// Client giữ bản plaintext.
/// </summary>
public class RefreshToken : BaseEntity
{
    public long UserId { get; set; }
    public string TokenHash { get; set; } = default!;   // SHA-256 của token thô
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }       // NULL = còn hiệu lực
    public string? ReplacedByTokenHash { get; set; }     // token thay thế khi xoay vòng (audit)

    public User User { get; set; } = default!;

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
