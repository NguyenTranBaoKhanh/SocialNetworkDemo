using Domain.Common;

namespace Domain.Entities;

public class Comment : BaseEntity
{
    public long PostId { get; set; }
    public long AuthorId { get; set; }
    public long? ParentId { get; set; }   // NULL = comment gốc; có giá trị = reply
    public string Content { get; set; } = default!;
    public int LikeCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation
    public Post Post { get; set; } = default!;
    public User Author { get; set; } = default!;
    public Comment? Parent { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
}
