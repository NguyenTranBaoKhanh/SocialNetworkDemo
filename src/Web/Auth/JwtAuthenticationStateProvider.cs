using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Web.Auth;

/// <summary>
/// Cung cấp trạng thái đăng nhập cho Blazor dựa trên access token trong localStorage.
/// Đọc claim từ payload JWT (chỉ để hiển thị UI — API mới là nơi xác thực thật sự).
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _tokens;
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    public JwtAuthenticationStateProvider(TokenStore tokens) => _tokens = tokens;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return Anonymous;

        var claims = ParseClaims(token);
        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>Gọi sau khi login/refresh thành công để UI cập nhật.</summary>
    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static IEnumerable<Claim> ParseClaims(string jwt)
    {
        try
        {
            var payload = jwt.Split('.')[1];
            var json = Base64UrlDecode(payload);
            var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (map is null) return [];

            var claims = new List<Claim>();
            foreach (var (key, value) in map)
            {
                var claimType = key switch
                {
                    // map tên claim JWT rút gọn sang ClaimTypes để [Authorize]/User đọc được
                    "nameid" or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
                        => ClaimTypes.NameIdentifier,
                    "unique_name" => ClaimTypes.Name,
                    _ => key,
                };
                claims.Add(new Claim(claimType, value.ToString()));
            }
            return claims;
        }
        catch
        {
            return [];
        }
    }

    private static string Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
