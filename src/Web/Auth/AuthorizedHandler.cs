using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Web.Models;

namespace Web.Auth;

/// <summary>
/// Gắn access token vào mỗi request. Khi API trả 401 (token hết hạn), tự động gọi
/// /api/auth/refresh để lấy token mới rồi thử LẠI request một lần. Nếu refresh thất bại
/// -> xóa token + báo đăng xuất.
/// </summary>
public class AuthorizedHandler : DelegatingHandler
{
    private readonly TokenStore _tokens;
    private readonly IHttpClientFactory _factory;
    private readonly JwtAuthenticationStateProvider _authState;

    public AuthorizedHandler(
        TokenStore tokens, IHttpClientFactory factory, JwtAuthenticationStateProvider authState)
    {
        _tokens = tokens;
        _factory = factory;
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        // Nhân bản request TRƯỚC khi gửi (để còn gửi lại nếu phải refresh).
        var clone = await CloneAsync(request);

        var access = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(access))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // 401 -> thử refresh.
        var refreshed = await TryRefreshAsync(ct);
        if (!refreshed)
        {
            await _tokens.ClearAsync();
            _authState.NotifyChanged();
            return response;   // trả 401 gốc cho caller xử lý (điều hướng về login)
        }

        // Gửi lại request với access token mới.
        var newAccess = await _tokens.GetAccessTokenAsync();
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccess);
        return await base.SendAsync(clone, ct);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        var refreshToken = await _tokens.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;

        // Dùng client "Api" (KHÔNG qua handler này) để tránh đệ quy.
        var api = _factory.CreateClient("Api");
        var res = await api.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(refreshToken), ct);
        if (!res.IsSuccessStatusCode) return false;

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
        if (auth is null) return false;

        await _tokens.SaveAsync(auth.Token, auth.RefreshToken);
        _authState.NotifyChanged();
        return true;
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        if (req.Content is not null)
        {
            var bytes = await req.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var h in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        return clone;
    }
}
