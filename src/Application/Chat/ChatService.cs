using Application.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Chat;

/// <summary>
/// Nghiệp vụ chat. Các method nhận userId TƯỜNG MINH (không dùng ICurrentUser) vì được
/// gọi cả từ SignalR hub — nơi không có HttpContext.
/// Nguyên tắc: persist trước khi ack; ordering bằng seq per conversation (không dùng client time).
/// </summary>
public class ChatService
{
    private readonly IAppDbContext _db;

    public ChatService(IAppDbContext db) => _db = db;

    /// <summary>Danh sách hội thoại của user (chat 1-1), sắp theo tin nhắn mới nhất.</summary>
    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(long userId, CancellationToken ct = default)
    {
        var memberships = await _db.ConversationMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.LeftAt == null)
            .Select(m => new { m.ConversationId, m.LastReadSeq })
            .ToListAsync(ct);

        var readByConv = memberships.ToDictionary(x => x.ConversationId, x => x.LastReadSeq);
        var convIds = memberships.Select(x => x.ConversationId).ToList();

        var rows = await _db.Conversations.AsNoTracking()
            .Where(c => convIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.PublicId,
                c.LastMessageAt,
                Other = c.Members.Where(m => m.UserId != userId)
                    .Select(m => new { m.User.Id, m.User.Username, m.User.DisplayName, m.User.AvatarUrl })
                    .FirstOrDefault(),
                LastMessage = c.Messages.Where(m => m.DeletedAt == null)
                    .OrderByDescending(m => m.Seq).Select(m => m.Content).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var result = new List<ConversationSummary>();
        foreach (var c in rows)
        {
            if (c.Other is null) continue;   // hội thoại hỏng (thiếu người kia)
            var lastRead = readByConv.GetValueOrDefault(c.Id);
            var unread = await _db.Messages.AsNoTracking()
                .CountAsync(m => m.ConversationId == c.Id && m.Seq > lastRead
                    && m.SenderId != userId && m.DeletedAt == null, ct);

            result.Add(new ConversationSummary(
                c.PublicId, c.Other.Id, c.Other.Username, c.Other.DisplayName, c.Other.AvatarUrl,
                c.LastMessage, c.LastMessageAt, unread));
        }

        return result.OrderByDescending(x => x.LastMessageAt ?? DateTimeOffset.MinValue).ToList();
    }

    /// <summary>Tìm hoặc tạo hội thoại 1-1 giữa user và người có username cho trước.</summary>
    public async Task<ConversationSummary> GetOrCreateDirectAsync(long userId, string otherUsername, CancellationToken ct = default)
    {
        var other = await _db.Users.FirstOrDefaultAsync(u => u.Username == otherUsername.Trim(), ct)
            ?? throw new NotFoundException("Không tìm thấy user.");
        if (other.Id == userId)
            throw new ValidationException("Không thể nhắn tin cho chính mình.");

        var key = DirectKey(userId, other.Id);
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.DirectKey == key, ct);

        if (conv is null)
        {
            conv = new Conversation { Type = "direct", DirectKey = key };
            conv.Members.Add(new ConversationMember { UserId = userId });
            conv.Members.Add(new ConversationMember { UserId = other.Id });
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync(ct);
        }

        return new ConversationSummary(
            conv.PublicId, other.Id, other.Username, other.DisplayName, other.AvatarUrl,
            null, conv.LastMessageAt, 0);
    }

    /// <summary>Lịch sử tin nhắn (cursor theo seq; trả về tăng dần để hiển thị).</summary>
    public async Task<CursorPage<MessageResponse>> GetMessagesAsync(
        long userId, Guid convPublicId, long? beforeSeq, int limit = 30, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var convId = await RequireMemberAsync(userId, convPublicId, ct);

        var query = _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == convId && m.DeletedAt == null);
        if (beforeSeq is long b) query = query.Where(m => m.Seq < b);

        var rows = await query
            .OrderByDescending(m => m.Seq)
            .Take(limit + 1)
            .Select(m => new MessageResponse(
                m.Id, m.Seq, m.SenderId, m.Sender.Username, m.Sender.DisplayName, m.Content,
                m.Attachments.Select(a => new MessageAttachmentDto(a.Url, a.MediaType)).ToList(), m.CreatedAt))
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            next = rows[^1].Seq.ToString();   // seq nhỏ nhất -> tải tin cũ hơn
        }

        rows.Reverse();   // hiển thị cũ -> mới
        return new CursorPage<MessageResponse>(rows, next);
    }

    /// <summary>Gửi tin nhắn (kèm ảnh/video nếu có): cấp seq, persist. Trả về message + thành viên để fan-out.</summary>
    public async Task<SendMessageResult> SendMessageAsync(
        long senderId, Guid convPublicId, string content,
        IReadOnlyList<AttachmentInput>? attachments, Guid? clientMsgId, CancellationToken ct = default)
    {
        content = (content ?? "").Trim();
        var atts = attachments ?? Array.Empty<AttachmentInput>();
        if (content.Length == 0 && atts.Count == 0)
            throw new ValidationException("Tin nhắn phải có nội dung hoặc đính kèm.");
        if (content.Length > 4000) throw new ValidationException("Tin nhắn tối đa 4000 ký tự.");

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.PublicId == convPublicId, ct)
            ?? throw new NotFoundException("Không tìm thấy hội thoại.");

        var members = await _db.ConversationMembers
            .Where(m => m.ConversationId == conv.Id).Select(m => m.UserId).ToListAsync(ct);
        if (!members.Contains(senderId))
            throw new ForbiddenException("Bạn không thuộc hội thoại này.");

        // Idempotency: cùng clientMsgId -> trả lại message đã có, không tạo trùng.
        if (clientMsgId is Guid cid)
        {
            var existed = await _db.Messages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.ConversationId == conv.Id && m.SenderId == senderId && m.ClientMsgId == cid, ct);
            if (existed is not null)
                return await BuildResultAsync(conv.PublicId, existed.Id, members, ct);
        }

        // Cấp seq (MVP: load + tăng; unique(conv,seq) chốt toàn vẹn nếu có race hiếm).
        var seq = conv.NextSeq;
        conv.NextSeq++;
        conv.LastMessageAt = DateTimeOffset.UtcNow;
        conv.UpdatedAt = DateTimeOffset.UtcNow;

        var msg = new Message
        {
            ConversationId = conv.Id,
            SenderId = senderId,
            Seq = seq,
            Content = content,
            ClientMsgId = clientMsgId,
        };
        foreach (var a in atts)
            msg.Attachments.Add(new MessageAttachment { Url = a.Url, MediaType = a.MediaType });

        _db.Messages.Add(msg);
        await _db.SaveChangesAsync(ct);

        return await BuildResultAsync(conv.PublicId, msg.Id, members, ct);
    }

    /// <summary>Đánh dấu đã đọc tới seq.</summary>
    public async Task MarkReadAsync(long userId, Guid convPublicId, long seq, CancellationToken ct = default)
    {
        var convId = await RequireMemberAsync(userId, convPublicId, ct);
        var member = await _db.ConversationMembers
            .FirstAsync(m => m.ConversationId == convId && m.UserId == userId, ct);
        if (seq > member.LastReadSeq)
        {
            member.LastReadSeq = seq;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<SendMessageResult> BuildResultAsync(
        Guid convPublicId, long messageId, IReadOnlyList<long> members, CancellationToken ct)
    {
        var dto = await _db.Messages.AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new MessageResponse(
                m.Id, m.Seq, m.SenderId, m.Sender.Username, m.Sender.DisplayName, m.Content,
                m.Attachments.Select(a => new MessageAttachmentDto(a.Url, a.MediaType)).ToList(), m.CreatedAt))
            .FirstAsync(ct);
        return new SendMessageResult(convPublicId, dto, members);
    }

    private async Task<long> RequireMemberAsync(long userId, Guid convPublicId, CancellationToken ct)
    {
        var conv = await _db.Conversations.AsNoTracking()
            .Where(c => c.PublicId == convPublicId)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy hội thoại.");

        var isMember = await _db.ConversationMembers
            .AnyAsync(m => m.ConversationId == conv.Id && m.UserId == userId, ct);
        if (!isMember) throw new ForbiddenException("Bạn không thuộc hội thoại này.");

        return conv.Id;
    }

    private static string DirectKey(long a, long b)
        => a < b ? $"{a}:{b}" : $"{b}:{a}";
}
