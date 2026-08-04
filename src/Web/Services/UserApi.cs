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

    public Task<UserProfileView?> GetProfileAsync(string username)
        => _http.GetFromJsonAsync<UserProfileView>($"api/users/{username}");

    public async Task<List<UserSummary>> GetSuggestionsAsync(int limit = 5)
        => await _http.GetFromJsonAsync<List<UserSummary>>($"api/users/suggestions?limit={limit}") ?? new();

    public async Task UpdateAvatarAsync(string url)
    {
        var res = await _http.PutAsJsonAsync("api/users/me/avatar", new UpdateAvatarRequest(url));
        res.EnsureSuccessStatusCode();
    }

    /// <summary>Cập nhật tên hiển thị + bio. Trả về null nếu thành công, hoặc thông báo lỗi.</summary>
    public async Task<string?> UpdateProfileAsync(string displayName, string bio)
    {
        var res = await _http.PutAsJsonAsync("api/users/me", new UpdateProfileRequest(displayName, bio));
        return res.IsSuccessStatusCode ? null : await ReadDetailAsync(res);
    }

    /// <summary>Đổi mật khẩu. Trả về null nếu thành công, hoặc thông báo lỗi.</summary>
    public async Task<string?> ChangePasswordAsync(string current, string newPassword)
    {
        var res = await _http.PostAsJsonAsync("api/users/me/password",
            new ChangePasswordRequest(current, newPassword));
        return res.IsSuccessStatusCode ? null : await ReadDetailAsync(res);
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage res)
    {
        try
        {
            var err = await res.Content.ReadFromJsonAsync<ProblemDetailsLite>();
            if (!string.IsNullOrWhiteSpace(err?.Detail)) return err!.Detail!;
        }
        catch { /* body không phải ProblemDetails */ }
        return $"Lỗi {(int)res.StatusCode}.";
    }

    private record ProblemDetailsLite(string? Detail);

    public async Task FollowAsync(string username)
        => (await _http.PostAsync($"api/users/{username}/follow", null)).EnsureSuccessStatusCode();

    public async Task UnfollowAsync(string username)
        => (await _http.DeleteAsync($"api/users/{username}/follow")).EnsureSuccessStatusCode();
}
