using Application.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Posts;

public class PostService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;

    public PostService(IAppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PostResponse> CreateAsync(CreatePostRequest req, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var content = (req.Content ?? "").Trim();
        var mediaCount = req.Media?.Count ?? 0;
        if (content.Length == 0 && mediaCount == 0)
            throw new ValidationException("Post phải có nội dung hoặc media.");
        if (content.Length > 5000)
            throw new ValidationException("Nội dung tối đa 5000 ký tự.");

        var post = new Post { AuthorId = userId, Content = content };

        short pos = 0;
        foreach (var m in req.Media ?? [])
        {
            post.Media.Add(new PostMedia
            {
                Url = m.Url,
                MediaType = m.MediaType,
                Width = m.Width,
                Height = m.Height,
                Position = pos++,
            });
        }

        _db.Posts.Add(post);

        // Counter cache trên user (đơn giản; sau này chuyển sang Redis/worker).
        var author = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        author.PostCount++;

        await _db.SaveChangesAsync(ct);

        return await GetByPublicIdAsync(post.PublicId, ct);
    }

    public async Task<PostResponse> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default)
    {
        var meId = _current.Id;

        var post = await _db.Posts
            .AsNoTracking()
            .Where(p => p.PublicId == publicId && p.DeletedAt == null)
            .Select(p => new
            {
                p.PublicId,
                p.Content,
                p.LikeCount,
                p.CommentCount,
                p.CreatedAt,
                Author = new AuthorDto(p.Author.Id, p.Author.Username, p.Author.DisplayName, p.Author.AvatarUrl),
                Media = p.Media.OrderBy(m => m.Position)
                    .Select(m => new MediaDto(m.Url, m.MediaType, m.Width, m.Height, m.Position)).ToList(),
                LikedByMe = meId != null && p.Likes.Any(l => l.UserId == meId),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy post.");

        return new PostResponse(post.PublicId, post.Author, post.Content, post.Media,
            post.LikeCount, post.CommentCount, post.LikedByMe, post.CreatedAt);
    }

    public async Task DeleteAsync(Guid publicId, CancellationToken ct = default)
    {
        var userId = RequireUser();

        var post = await _db.Posts.FirstOrDefaultAsync(
            p => p.PublicId == publicId && p.DeletedAt == null, ct)
            ?? throw new NotFoundException("Không tìm thấy post.");

        if (post.AuthorId != userId)
            throw new ForbiddenException("Chỉ tác giả mới được xóa post.");

        post.DeletedAt = DateTimeOffset.UtcNow;   // soft delete

        var author = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        if (author.PostCount > 0) author.PostCount--;

        await _db.SaveChangesAsync(ct);
    }

    private long RequireUser() =>
        _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");
}
