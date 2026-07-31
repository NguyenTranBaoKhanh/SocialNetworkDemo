namespace Domain.Entities;

/// <summary>
/// Quan hệ follow có hướng. Khóa kép (FollowerId, FolloweeId) chống double-follow;
/// CHECK ở tầng DB chống tự-follow.
/// </summary>
public class Follow
{
    public long FollowerId { get; set; }   // người đi follow
    public long FolloweeId { get; set; }   // người được follow
    public DateTimeOffset CreatedAt { get; set; }

    public User Follower { get; set; } = default!;
    public User Followee { get; set; } = default!;
}
