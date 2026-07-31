using Application.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Comments;

public class CommentService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;

    public CommentService(IAppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    public async Task<CommentResponse> AddAsync(
        Guid postPublicId, CreateCommentRequest req, CancellationToken ct = default)
    {
        var userId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var content = (req.Content ?? "").Trim();
        if (content.Length == 0)
            throw new ValidationException("Comment không được trống.");
        if (content.Length > 2000)
            throw new ValidationException("Comment tối đa 2000 ký tự.");

        var post = await _db.Posts.FirstOrDefaultAsync(
            p => p.PublicId == postPublicId && p.DeletedAt == null, ct)
            ?? throw new NotFoundException("Không tìm thấy post.");

        // Reply: parent phải thuộc cùng post.
        if (req.ParentId is long parentId)
        {
            var parentOk = await _db.Comments.AnyAsync(
                c => c.Id == parentId && c.PostId == post.Id && c.DeletedAt == null, ct);
            if (!parentOk)
                throw new ValidationException("Comment cha không hợp lệ.");
        }

        var comment = new Comment
        {
            PostId = post.Id,
            AuthorId = userId,
            ParentId = req.ParentId,
            Content = content,
        };
        _db.Comments.Add(comment);
        post.CommentCount++;   // counter cache
        await _db.SaveChangesAsync(ct);

        var author = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AuthorDto(u.Id, u.Username, u.DisplayName, u.AvatarUrl))
            .FirstAsync(ct);

        return new CommentResponse(comment.Id, comment.ParentId, author,
            comment.Content, 0, comment.CreatedAt);
    }

    /// <summary>Liệt kê comment của post, cũ → mới, cursor theo Id.</summary>
    public async Task<CursorPage<CommentResponse>> ListAsync(
        Guid postPublicId, long? afterId, int limit = 20, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var postId = await _db.Posts
            .Where(p => p.PublicId == postPublicId && p.DeletedAt == null)
            .Select(p => (long?)p.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy post.");

        var query = _db.Comments.AsNoTracking()
            .Where(c => c.PostId == postId && c.DeletedAt == null);

        if (afterId is long cursor)
            query = query.Where(c => c.Id > cursor);

        var items = await query
            .OrderBy(c => c.Id)
            .Take(limit + 1)   // lấy dư 1 để biết còn trang sau không
            .Select(c => new CommentResponse(
                c.Id,
                c.ParentId,
                new AuthorDto(c.Author.Id, c.Author.Username, c.Author.DisplayName, c.Author.AvatarUrl),
                c.Content,
                c.LikeCount,
                c.CreatedAt))
            .ToListAsync(ct);

        string? next = null;
        if (items.Count > limit)
        {
            next = items[^1].Id.ToString();
            items.RemoveAt(items.Count - 1);
        }

        return new CursorPage<CommentResponse>(items, next);
    }

    public async Task DeleteAsync(long commentId, CancellationToken ct = default)
    {
        var userId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var comment = await _db.Comments.FirstOrDefaultAsync(
            c => c.Id == commentId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("Không tìm thấy comment.");

        if (comment.AuthorId != userId)
            throw new ForbiddenException("Chỉ tác giả mới được xóa comment.");

        comment.DeletedAt = DateTimeOffset.UtcNow;

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == comment.PostId, ct);
        if (post is not null && post.CommentCount > 0) post.CommentCount--;

        await _db.SaveChangesAsync(ct);
    }
}
