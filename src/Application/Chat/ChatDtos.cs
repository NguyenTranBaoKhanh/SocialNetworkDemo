namespace Application.Chat;

public record MessageAttachmentDto(string Url, string MediaType);

/// <summary>Một tin nhắn (kèm ảnh/video nếu có).</summary>
public record MessageResponse(
    long Id,
    long Seq,
    long SenderId,
    string SenderUsername,
    string SenderDisplayName,
    string Content,
    List<MessageAttachmentDto> Attachments,
    DateTimeOffset CreatedAt);

/// <summary>Tóm tắt 1 hội thoại cho danh sách (chat 1-1: hiển thị người kia).</summary>
public record ConversationSummary(
    Guid Id,
    long OtherUserId,
    string OtherUsername,
    string OtherDisplayName,
    string? OtherAvatarUrl,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    int UnreadCount);

/// <summary>Media đính kèm khi gửi (url đã upload qua /api/media).</summary>
public record AttachmentInput(string Url, string MediaType);

/// <summary>Kết quả gửi tin: message + danh sách thành viên (để hub fan-out).</summary>
public record SendMessageResult(Guid ConversationId, MessageResponse Message, IReadOnlyList<long> MemberIds);
