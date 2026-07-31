namespace Domain.Entities;

/// <summary>
/// Thành viên hội thoại. Con trỏ đọc/nhận đóng vai trò trạng thái read/delivered:
/// unread = message có Seq > LastReadSeq. Rẻ hơn ghi receipt mỗi message.
/// </summary>
public class ConversationMember
{
    public long ConversationId { get; set; }
    public long UserId { get; set; }
    public string Role { get; set; } = "member";   // 'owner' | 'admin' | 'member'
    public long LastReadSeq { get; set; }           // đã đọc tới seq này
    public long LastDeliveredSeq { get; set; }      // đã nhận tới seq này
    public bool IsMuted { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }     // NULL = còn trong hội thoại

    public Conversation Conversation { get; set; } = default!;
    public User User { get; set; } = default!;
}
