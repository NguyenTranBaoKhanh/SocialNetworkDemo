using Domain.Common;

namespace Domain.Entities;

public class Message : BaseEntity
{
    public long ConversationId { get; set; }
    public long SenderId { get; set; }
    public long Seq { get; set; }                  // thứ tự trong hội thoại (unique per conversation)
    public string Content { get; set; } = string.Empty;

    // Id tạm do client sinh: chống gửi trùng khi retry (idempotency) + map optimistic UI.
    public Guid? ClientMsgId { get; set; }

    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public Conversation Conversation { get; set; } = default!;
    public User Sender { get; set; } = default!;
    public ICollection<MessageAttachment> Attachments { get; set; } = new List<MessageAttachment>();
}
