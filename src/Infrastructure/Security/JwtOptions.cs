namespace Infrastructure.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string Key { get; set; } = default!;
    public int ExpiryMinutes { get; set; } = 15;        // access token sống ngắn (15 phút)
    public int RefreshTokenDays { get; set; } = 7;      // refresh token sống dài (7 ngày)
}
