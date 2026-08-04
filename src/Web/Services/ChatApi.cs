using System.Net.Http.Json;
using Web.Models;

namespace Web.Services;

/// <summary>REST cho chat (danh sách hội thoại, lịch sử, bắt đầu chat). Gửi tin realtime dùng SignalR.</summary>
public class ChatApi
{
    private readonly HttpClient _http;

    public ChatApi(IHttpClientFactory factory) => _http = factory.CreateClient("AuthorizedApi");

    public async Task<List<ConversationSummary>> GetConversationsAsync()
        => await _http.GetFromJsonAsync<List<ConversationSummary>>("api/conversations") ?? new();

    public async Task<ConversationSummary?> StartDirectAsync(string username)
    {
        var res = await _http.PostAsync($"api/conversations/direct/{username}", null);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<ConversationSummary>();
    }

    public async Task<CursorPage<MessageResponse>?> GetMessagesAsync(Guid conversationId, long? before = null, int limit = 30)
    {
        var url = $"api/conversations/{conversationId}/messages?limit={limit}";
        if (before is not null) url += $"&before={before}";
        return await _http.GetFromJsonAsync<CursorPage<MessageResponse>>(url);
    }

    public async Task MarkReadAsync(Guid conversationId, long seq)
        => await _http.PostAsync($"api/conversations/{conversationId}/read?seq={seq}", null);
}
