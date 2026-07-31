using Application.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Likes;

public record LikeResult(int LikeCount, bool LikedByMe);

public class LikeService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;

    public LikeService(IAppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>Like một post. Idempotent — like lại không tăng đúp (khóa kép ở DB chốt).</summary>
    public async Task<LikeResult> LikeAsync(Guid postPublicId, CancellationToken ct = default)
    {
        var userId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var post = await _db.Posts.FirstOrDefaultAsync(
            p => p.PublicId == postPublicId && p.DeletedAt == null, ct)
            ?? throw new NotFoundException("Không tìm thấy post.");

        var already = await _db.Likes.AnyAsync(l => l.UserId == userId && l.PostId == post.Id, ct);
        if (!already)
        {
            _db.Likes.Add(new Like { UserId = userId, PostId = post.Id });
            post.LikeCount++;   // counter cache (MVP; sau chuyển Redis INCR + worker flush)
            await _db.SaveChangesAsync(ct);
        }

        return new LikeResult(post.LikeCount, true);
    }

    public async Task<LikeResult> UnlikeAsync(Guid postPublicId, CancellationToken ct = default)
    {
        var userId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var post = await _db.Posts.FirstOrDefaultAsync(
            p => p.PublicId == postPublicId && p.DeletedAt == null, ct)
            ?? throw new NotFoundException("Không tìm thấy post.");

        var like = await _db.Likes.FirstOrDefaultAsync(
            l => l.UserId == userId && l.PostId == post.Id, ct);
        if (like is not null)
        {
            _db.Likes.Remove(like);
            if (post.LikeCount > 0) post.LikeCount--;
            await _db.SaveChangesAsync(ct);
        }

        return new LikeResult(post.LikeCount, false);
    }
}
