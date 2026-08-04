using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Web.Auth;
using Web.Models;

namespace Web.Services;

/// <summary>
/// Kết nối chat DÙNG CHUNG toàn app (scoped = sống suốt phiên WASM). Giữ:
///  - HubConnection (SignalR) mở liên tục khi đã đăng nhập,
///  - danh sách hội thoại + tổng unread (để sidebar hiện badge),
///  - tập user online.
/// MainLayout gọi EnsureStartedAsync khi đăng nhập; trang Messages đọc chung state này.
/// </summary>
public class ChatConnection : IAsyncDisposable
{
    private readonly ChatApi _api;
    private readonly TokenStore _tokens;
    private readonly ClientSettings _settings;
    private readonly AuthenticationStateProvider _auth;

    private HubConnection? _hub;
    private long _myId;
    private bool _starting;

    public ChatConnection(ChatApi api, TokenStore tokens, ClientSettings settings, AuthenticationStateProvider auth)
    {
        _api = api;
        _tokens = tokens;
        _settings = settings;
        _auth = auth;
    }

    public List<ConversationSummary> Conversations { get; private set; } = new();
    public HashSet<long> Online { get; } = new();
    public Guid? ActiveConversationId { get; set; }   // hội thoại đang mở (không tính unread)

    public int TotalUnread => Conversations.Sum(c => c.UnreadCount);

    /// <summary>UI re-render khi có thay đổi (unread/online/conversation).</summary>
    public event Action? Changed;
    /// <summary>Trang Messages append tin khi có tin mới cho hội thoại đang mở.</summary>
    public event Action<MessageReceived>? MessageArrived;

    /// <summary>Kết nối hub + nạp danh sách hội thoại (idempotent).</summary>
    public async Task EnsureStartedAsync()
    {
        if (_hub is not null || _starting) return;
        _starting = true;

        var state = await _auth.GetAuthenticationStateAsync();
        long.TryParse(state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out _myId);

        Conversations = await _api.GetConversationsAsync();
        Changed?.Invoke();

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_settings.ApiBaseUrl}/hubs/chat", o =>
                o.AccessTokenProvider = async () => await _tokens.GetAccessTokenAsync())
            .WithAutomaticReconnect()
            .Build();

        _hub.On<MessageReceived>("MessageReceived", OnMessageReceived);
        _hub.On<PresenceChange>("PresenceChanged", p =>
        {
            if (p.Online) Online.Add(p.UserId); else Online.Remove(p.UserId);
            Changed?.Invoke();
        });
        _hub.On<List<long>>("OnlineUsers", ids =>
        {
            Online.Clear();
            foreach (var id in ids) Online.Add(id);
            Changed?.Invoke();
        });

        try { await _hub.StartAsync(); } catch { /* WithAutomaticReconnect sẽ thử lại */ }
        _starting = false;
    }

    public Task SendMessageAsync(Guid conversationId, string content, IReadOnlyList<CreateMediaDto>? attachments = null)
    {
        var atts = attachments?.Select(a => new { url = a.Url, mediaType = a.MediaType }).ToList();
        return _hub?.InvokeAsync("SendMessage", conversationId, content, atts, Guid.NewGuid()) ?? Task.CompletedTask;
    }

    public async Task MarkReadAsync(Guid conversationId, long seq)
    {
        await _api.MarkReadAsync(conversationId, seq);
        SetUnread(conversationId, 0);
        Changed?.Invoke();
    }

    private async void OnMessageReceived(MessageReceived e)
    {
        var idx = Conversations.FindIndex(c => c.Id == e.ConversationId);
        if (idx < 0)
        {
            // Hội thoại mới -> nạp lại danh sách.
            Conversations = await _api.GetConversationsAsync();
        }
        else
        {
            var conv = Conversations[idx];
            var unread = conv.UnreadCount;
            if (e.ConversationId != ActiveConversationId && e.Message.SenderId != _myId)
                unread++;
            var updated = conv with
            {
                LastMessage = e.Message.Content,
                LastMessageAt = e.Message.CreatedAt,
                UnreadCount = unread,
            };
            Conversations.RemoveAt(idx);
            Conversations.Insert(0, updated);   // đưa lên đầu
        }

        MessageArrived?.Invoke(e);
        Changed?.Invoke();
    }

    private void SetUnread(Guid conversationId, int count)
    {
        var i = Conversations.FindIndex(c => c.Id == conversationId);
        if (i >= 0) Conversations[i] = Conversations[i] with { UnreadCount = count };
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }
}
