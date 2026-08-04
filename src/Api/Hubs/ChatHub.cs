using System.Security.Claims;
using Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// Hub chat realtime. Fan-out theo group "user:{id}" (mỗi connection của user vào group của user đó),
/// nên tin nhắn tới mọi thành viên hội thoại dù họ có đang mở hội thoại đó hay không.
/// Persist qua ChatService TRƯỚC khi broadcast (đúng nguyên tắc CLAUDE.md).
/// Nhiều instance => cần Redis backplane (đã bật sẵn ở Program.cs khi có Redis).
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ChatService _chat;
    private readonly PresenceTracker _presence;

    public ChatHub(ChatService chat, PresenceTracker presence)
    {
        _chat = chat;
        _presence = presence;
    }

    private long UserId =>
        long.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private static string UserGroup(long userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var uid = UserId;
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(uid));

        // Gửi danh sách đang online cho người vừa kết nối.
        await Clients.Caller.SendAsync("OnlineUsers", _presence.OnlineUsers());

        // Nếu vừa chuyển sang online -> báo mọi người.
        if (_presence.Connect(uid))
            await Clients.Others.SendAsync("PresenceChanged", new { userId = uid, online = true });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var uid = UserId;
        if (_presence.Disconnect(uid))
            await Clients.Others.SendAsync("PresenceChanged", new { userId = uid, online = false });

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Gửi tin (kèm ảnh/video): persist rồi fan-out tới mọi thành viên (kể cả người gửi).</summary>
    public async Task<MessageResponse> SendMessage(
        Guid conversationId, string content, List<AttachmentInput>? attachments, Guid? clientMsgId)
    {
        var result = await _chat.SendMessageAsync(UserId, conversationId, content, attachments, clientMsgId);

        foreach (var memberId in result.MemberIds)
        {
            await Clients.Group(UserGroup(memberId)).SendAsync("MessageReceived",
                new { conversationId = result.ConversationId, message = result.Message });
        }

        return result.Message;
    }

    /// <summary>Đánh dấu đã đọc tới seq.</summary>
    public Task MarkRead(Guid conversationId, long seq) => _chat.MarkReadAsync(UserId, conversationId, seq);
}
