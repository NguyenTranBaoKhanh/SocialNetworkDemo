using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common;

/// <summary>
/// Cổng truy cập dữ liệu cho Application. Infrastructure hiện thực bằng EF Core AppDbContext.
/// Giữ Application không phụ thuộc trực tiếp vào Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Post> Posts { get; }
    DbSet<PostMedia> PostMedia { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Like> Likes { get; }
    DbSet<Follow> Follows { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationMember> ConversationMembers { get; }
    DbSet<Message> Messages { get; }
    DbSet<MessageAttachment> MessageAttachments { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
