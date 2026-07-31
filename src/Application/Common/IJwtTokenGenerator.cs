using Domain.Entities;

namespace Application.Common;

public interface IJwtTokenGenerator
{
    /// <summary>Sinh JWT access token cho user; trả về (token, thời điểm hết hạn).</summary>
    (string Token, DateTimeOffset ExpiresAt) Generate(User user);
}
