using Domain.Common;

namespace Domain.Entities;

public class Post : BaseEntity
{
    public Guid PublicId { get; set; }
    public long AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;

    // Counter cache — flush từ Redis INCR. KHÔNG UPDATE +1 mỗi request.
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }   // soft delete

    // Navigation
    public User Author { get; set; } = default!;
    public ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
}
