using System.Net.Http.Json;
using Web.Auth;
using Web.Models;

namespace Web.Services;

/// <summary>Gọi các endpoint auth. Dùng client "Api" thuần (không cần Bearer).</summary>
public class AuthApi
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;
    private readonly JwtAuthenticationStateProvider _authState;

    public AuthApi(IHttpClientFactory factory, TokenStore tokens, JwtAuthenticationStateProvider authState)
    {
        _http = factory.CreateClient("Api");
        _tokens = tokens;
        _authState = authState;
    }

    public async Task<string?> RegisterAsync(RegisterRequest req)
        => await AuthenticateAsync("api/auth/register", req);

    public async Task<string?> LoginAsync(LoginRequest req)
        => await AuthenticateAsync("api/auth/login", req);

    public async Task LogoutAsync()
    {
        var refresh = await _tokens.GetRefreshTokenAsync();
        if (!string.IsNullOrWhiteSpace(refresh))
        {
            try { await _http.PostAsJsonAsync("api/auth/logout", new LogoutRequest(refresh)); }
            catch { /* logout local dù API lỗi */ }
        }
        await _tokens.ClearAsync();
        _authState.NotifyChanged();
    }

    /// <summary>Trả về null nếu thành công, hoặc thông báo lỗi nếu thất bại.</summary>
    private async Task<string?> AuthenticateAsync<TReq>(string url, TReq req)
    {
        var res = await _http.PostAsJsonAsync(url, req);
        if (!res.IsSuccessStatusCode)
            return await ReadErrorAsync(res);

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth is null) return "Phản hồi không hợp lệ từ máy chủ.";

        await _tokens.SaveAsync(auth.Token, auth.RefreshToken);
        _authState.NotifyChanged();
        return null;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage res)
    {
        try
        {
            var problem = await res.Content.ReadFromJsonAsync<ProblemDetailsLite>();
            if (!string.IsNullOrWhiteSpace(problem?.Detail)) return problem!.Detail!;
        }
        catch { /* body không phải ProblemDetails */ }
        return $"Lỗi {(int)res.StatusCode}.";
    }

    private record ProblemDetailsLite(string? Title, string? Detail, int? Status);
}
