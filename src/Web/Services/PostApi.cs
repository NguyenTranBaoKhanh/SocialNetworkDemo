using System.Net.Http.Json;
using Web.Models;

namespace Web.Services;

/// <summary>Gọi các endpoint cần đăng nhập. Dùng client "AuthorizedApi" (tự gắn Bearer + refresh).</summary>
public class PostApi
{
    private readonly HttpClient _http;

    public PostApi(IHttpClientFactory factory) => _http = factory.CreateClient("AuthorizedApi");

    public async Task<CursorPage<PostResponse>?> GetFeedAsync(string? cursor = null, int limit = 20)
    {
        var url = $"api/feed?limit={limit}";
        if (!string.IsNullOrWhiteSpace(cursor))
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        return await _http.GetFromJsonAsync<CursorPage<PostResponse>>(url);
    }

    public async Task<PostResponse?> GetPostAsync(Guid id)
        => await _http.GetFromJsonAsync<PostResponse>($"api/posts/{id}");

    public async Task<PostResponse?> CreatePostAsync(string content)
    {
        var res = await _http.PostAsJsonAsync("api/posts", new CreatePostRequest(content, null));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<PostResponse>();
    }

    public async Task<LikeResult?> LikeAsync(Guid postId)
    {
        var res = await _http.PostAsync($"api/posts/{postId}/like", null);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<LikeResult>();
    }

    public async Task<LikeResult?> UnlikeAsync(Guid postId)
    {
        var res = await _http.DeleteAsync($"api/posts/{postId}/like");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<LikeResult>();
    }

    public async Task<CursorPage<CommentResponse>?> GetCommentsAsync(Guid postId, long? after = null, int limit = 20)
    {
        var url = $"api/posts/{postId}/comments?limit={limit}";
        if (after is not null) url += $"&after={after}";
        return await _http.GetFromJsonAsync<CursorPage<CommentResponse>>(url);
    }

    public async Task<CommentResponse?> AddCommentAsync(Guid postId, string content, long? parentId = null)
    {
        var res = await _http.PostAsJsonAsync($"api/posts/{postId}/comments",
            new CreateCommentRequest(content, parentId));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CommentResponse>();
    }
}
