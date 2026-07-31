namespace Domain.Entities;

/// <summary>
/// Nguồn sự thật của like. Khóa chính kép (UserId, PostId) chống double-like ở tầng DB.
/// Post.LikeCount chỉ là cache.
/// </summary>
public class Like
{
    public long UserId { get; set; }
    public long PostId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = default!;
    public Post Post { get; set; } = default!;
}
