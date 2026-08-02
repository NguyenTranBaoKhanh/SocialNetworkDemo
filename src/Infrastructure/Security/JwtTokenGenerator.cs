using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Common;
using Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _opt;

    public JwtTokenGenerator(IOptions<JwtOptions> opt) => _opt = opt.Value;

    public (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_opt.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public RefreshTokenResult GenerateRefreshToken()
    {
        // Token thô: 32 byte ngẫu nhiên mã hóa base64url (không đoán được).
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Base64UrlEncode(bytes);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_opt.RefreshTokenDays);
        return new RefreshTokenResult(raw, HashRefreshToken(raw), expiresAt);
    }

    public string HashRefreshToken(string rawToken)
    {
        // SHA-256 deterministic để tra cứu được (khác password hash có salt).
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
