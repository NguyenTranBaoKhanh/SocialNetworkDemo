using Application.Chat;
using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/conversations")]
public class ConversationsController : ControllerBase
{
    private readonly ChatService _chat;
    private readonly ICurrentUser _current;

    public ConversationsController(ChatService chat, ICurrentUser current)
    {
        _chat = chat;
        _current = current;
    }

    private long Me => _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

    /// <summary>Danh sách hội thoại của tôi.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _chat.GetConversationsAsync(Me, ct));

    /// <summary>Tìm/tạo hội thoại 1-1 với 1 user (bắt đầu nhắn tin).</summary>
    [HttpPost("direct/{username}")]
    public async Task<ActionResult<ConversationSummary>> Direct(string username, CancellationToken ct)
        => Ok(await _chat.GetOrCreateDirectAsync(Me, username, ct));

    /// <summary>Lịch sử tin nhắn của 1 hội thoại (cursor theo seq: ?before=).</summary>
    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(
        Guid id, [FromQuery] long? before, [FromQuery] int limit = 30, CancellationToken ct = default)
        => Ok(await _chat.GetMessagesAsync(Me, id, before, limit, ct));

    /// <summary>Đánh dấu đã đọc tới seq (dùng khi mở hội thoại).</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, [FromQuery] long seq, CancellationToken ct)
    {
        await _chat.MarkReadAsync(Me, id, seq, ct);
        return NoContent();
    }
}
