using Application.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Extension cần cho default value ở tầng DB.
        b.HasPostgresExtension("uuid-ossp");   // uuid_generate_v4()
        b.HasPostgresExtension("citext");      // so sánh username/email không phân biệt hoa thường

        // ---------------- USERS ----------------
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.PublicId).HasDefaultValueSql("uuid_generate_v4()");
            e.Property(x => x.Username).HasColumnType("citext").IsRequired();
            e.Property(x => x.Email).HasColumnType("citext").IsRequired();
            e.Property(x => x.Bio).HasDefaultValue(string.Empty);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.PublicId).IsUnique();
        });

        // ---------------- REFRESH TOKENS ----------------
        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Ignore(x => x.IsActive);   // computed, không map cột
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_refresh_tokens_user");
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- POSTS ----------------
        b.Entity<Post>(e =>
        {
            e.ToTable("posts");
            e.HasKey(x => x.Id);
            e.Property(x => x.PublicId).HasDefaultValueSql("uuid_generate_v4()");
            e.Property(x => x.Content).HasDefaultValue(string.Empty);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.PublicId).IsUnique();
            // Feed fan-out on read: lọc theo author, sắp theo thời gian, bỏ post đã xóa.
            e.HasIndex(x => new { x.AuthorId, x.CreatedAt })
                .HasFilter("deleted_at IS NULL")
                .HasDatabaseName("idx_posts_author_created");

            e.HasOne(x => x.Author).WithMany(u => u.Posts)
                .HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- POST MEDIA ----------------
        b.Entity<PostMedia>(e =>
        {
            e.ToTable("post_media");
            e.HasKey(x => x.Id);
            e.Property(x => x.MediaType).HasDefaultValue("image");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.PostId, x.Position });
            e.HasOne(x => x.Post).WithMany(p => p.Media)
                .HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- COMMENTS ----------------
        b.Entity<Comment>(e =>
        {
            e.ToTable("comments");
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.PostId, x.CreatedAt })
                .HasFilter("deleted_at IS NULL")
                .HasDatabaseName("idx_comments_post_created");
            e.HasIndex(x => x.ParentId).HasDatabaseName("idx_comments_parent");

            e.HasOne(x => x.Post).WithMany(p => p.Comments)
                .HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Author).WithMany()
                .HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Parent).WithMany(c => c.Replies)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- LIKES (khóa kép chống double-like) ----------------
        b.Entity<Like>(e =>
        {
            e.ToTable("likes");
            e.HasKey(x => new { x.UserId, x.PostId });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.PostId).HasDatabaseName("idx_likes_post");
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Post).WithMany(p => p.Likes)
                .HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- FOLLOWS (khóa kép + chống tự-follow) ----------------
        b.Entity<Follow>(e =>
        {
            e.ToTable("follows");
            e.HasKey(x => new { x.FollowerId, x.FolloweeId });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.ToTable(t => t.HasCheckConstraint("chk_follows_no_self", "follower_id <> followee_id"));
            e.HasIndex(x => x.FollowerId).HasDatabaseName("idx_follows_follower");
            e.HasIndex(x => x.FolloweeId).HasDatabaseName("idx_follows_followee");

            e.HasOne(x => x.Follower).WithMany(u => u.Following)
                .HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Followee).WithMany(u => u.Followers)
                .HasForeignKey(x => x.FolloweeId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- CONVERSATIONS ----------------
        b.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.PublicId).HasDefaultValueSql("uuid_generate_v4()");
            e.Property(x => x.Type).HasDefaultValue("direct");
            e.Property(x => x.NextSeq).HasDefaultValue(1L);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.ToTable(t => t.HasCheckConstraint("chk_conversations_type", "type IN ('direct','group')"));
            e.HasIndex(x => x.PublicId).IsUnique();
            e.HasIndex(x => x.DirectKey).IsUnique();  // chống tạo trùng hội thoại 1-1
            e.HasIndex(x => x.LastMessageAt).HasDatabaseName("idx_conversations_last_message");
        });

        // ---------------- CONVERSATION MEMBERS ----------------
        b.Entity<ConversationMember>(e =>
        {
            e.ToTable("conversation_members");
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.Property(x => x.Role).HasDefaultValue("member");
            e.Property(x => x.JoinedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.UserId)
                .HasFilter("left_at IS NULL")
                .HasDatabaseName("idx_conv_members_user");
            e.HasOne(x => x.Conversation).WithMany(c => c.Members)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- MESSAGES (ordering bằng seq) ----------------
        b.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasDefaultValue(string.Empty);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.ConversationId, x.Seq })
                .IsUnique().HasDatabaseName("uq_messages_conv_seq");
            // Idempotency: chống gửi trùng khi client retry.
            e.HasIndex(x => new { x.ConversationId, x.SenderId, x.ClientMsgId })
                .IsUnique()
                .HasFilter("client_msg_id IS NOT NULL")
                .HasDatabaseName("uq_messages_client_id");
            e.HasIndex(x => new { x.ConversationId, x.Seq })
                .HasFilter("deleted_at IS NULL")
                .HasDatabaseName("idx_messages_conv_seq");

            e.HasOne(x => x.Conversation).WithMany(c => c.Messages)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany()
                .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- MESSAGE ATTACHMENTS ----------------
        b.Entity<MessageAttachment>(e =>
        {
            e.ToTable("message_attachments");
            e.HasKey(x => x.Id);
            e.Property(x => x.MediaType).HasDefaultValue("image");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.MessageId).HasDatabaseName("idx_message_attachments_message");
            e.HasOne(x => x.Message).WithMany(m => m.Attachments)
                .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        // snake_case cho toàn bộ cột (khớp schema.sql).
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
                prop.SetColumnName(ToSnakeCase(prop.GetColumnName()));
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && !char.IsUpper(input[i - 1])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
