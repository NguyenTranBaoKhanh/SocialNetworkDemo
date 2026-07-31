using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// Hub chat realtime. Nguyên tắc (CLAUDE.md):
///  - Group theo conversationId để fan-out tin nhắn.
///  - Persist message vào DB TRƯỚC khi ack + broadcast (làm trong service, chưa nối ở đây).
///  - Ordering bằng seq per conversation — server cấp, không tin client.
///  - Nhiều instance => BẮT BUỘC Redis backplane (bật ở Program.cs).
///
/// Đây là khung: các method thao tác DB (gửi tin, đánh dấu đã đọc) sẽ gọi sang
/// Application service ở bước sau. Hiện tại lo phần join/leave group + typing.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private static string GroupName(long conversationId) => $"conversation:{conversationId}";

    private long CurrentUserId =>
        long.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    /// <summary>Tham gia nhóm SignalR của một hội thoại để nhận tin realtime.</summary>
    public async Task JoinConversation(long conversationId)
    {
        // TODO: kiểm tra CurrentUserId có phải thành viên hội thoại không (authz).
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    public async Task LeaveConversation(long conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    /// <summary>Typing indicator — chỉ broadcast, KHÔNG persist DB (đúng nguyên tắc).</summary>
    public async Task Typing(long conversationId)
    {
        await Clients.OthersInGroup(GroupName(conversationId))
            .SendAsync("UserTyping", new { conversationId, userId = CurrentUserId });
    }

    // TODO (bước sau, gọi Application service):
    //   Task<MessageDto> SendMessage(long conversationId, string content, Guid clientMsgId)
    //     -> service persist (cấp seq trong transaction) -> commit
    //     -> Clients.Group(...).SendAsync("MessageReceived", dto)
    //   Task MarkRead(long conversationId, long seq)
    //     -> cập nhật last_read_seq -> báo "ReadReceipt" cho nhóm.
}
