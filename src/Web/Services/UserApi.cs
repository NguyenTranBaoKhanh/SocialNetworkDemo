using System.Net.Http.Json;
using Web.Models;

namespace Web.Services;

/// <summary>Gọi API user (cần đăng nhập).</summary>
public class UserApi
{
    private readonly HttpClient _http;

    public UserApi(IHttpClientFactory factory) => _http = factory.CreateClient("AuthorizedApi");

    /// <summary>Thông tin user đang đăng nhập; null nếu chưa đăng nhập / lỗi.</summary>
    public async Task<UserProfile?> GetMeAsync()
    {
        try { return await _http.GetFromJsonAsync<UserProfile>("api/users/me"); }
        catch { return null; }
    }
}
