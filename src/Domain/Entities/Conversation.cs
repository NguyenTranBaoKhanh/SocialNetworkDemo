using Domain.Common;

namespace Domain.Entities;

public class Conversation : BaseEntity
{
    public Guid PublicId { get; set; }
    public string Type { get; set; } = "direct";   // 'direct' | 'group'
    public string? Title { get; set; }             // chỉ dùng cho group

    // Khóa duy nhất cho hội thoại 1-1: 'min(a,b):max(a,b)'. NULL với group.
    public string? DirectKey { get; set; }

    // Bộ đếm sinh seq cho message kế tiếp. Ordering dựa vào đây, KHÔNG dùng client time.
    public long NextSeq { get; set; } = 1;

    public DateTimeOffset? LastMessageAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
