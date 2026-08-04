using Application.Common;
using Application.Posts;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Feed;

/// <summary>
/// Feed fan-out on READ (đúng khuyến nghị MVP của CLAUDE.md): lúc mở app mới query
/// post của những người user follow (+ post của chính mình), sắp mới nhất trước.
/// Cursor pagination theo (CreatedAt, Id) để ổn định khi có post trùng thời điểm.
/// </summary>
public class FeedService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;

    public FeedService(IAppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>Feed của user hiện tại: bài của người mình follow + của chính mình.</summary>
    public async Task<CursorPage<PostResponse>> GetAsync(
        string? cursor, int limit = 20, CancellationToken ct = default)
    {
        var meId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var authorIds = await _db.Follows
            .Where(f => f.FollowerId == meId)
            .Select(f => f.FolloweeId)
            .ToListAsync(ct);
        authorIds.Add(meId);

        var baseQuery = _db.Posts.AsNoTracking()
            .Where(p => p.DeletedAt == null && authorIds.Contains(p.AuthorId));

        return await PageAsync(baseQuery, cursor, limit, meId, ct);
    }

    /// <summary>Bài của một user theo username (trang profile).</summary>
    public async Task<CursorPage<PostResponse>> GetUserPostsAsync(
        string username, string? cursor, int limit = 20, CancellationToken ct = default)
    {
        var meId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var authorId = await _db.Users
            .Where(u => u.Username == username.Trim())
            .Select(u => (long?)u.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        var baseQuery = _db.Posts.AsNoTracking()
            .Where(p => p.DeletedAt == null && p.AuthorId == authorId);

        return await PageAsync(baseQuery, cursor, limit, meId, ct);
    }

    /// <summary>Phân trang cursor + projection dùng chung.</summary>
    private async Task<CursorPage<PostResponse>> PageAsync(
        IQueryable<Post> baseQuery, string? cursor, int limit, long meId, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 50);

        // Giải mã cursor "createdAtTicks_id" -> lấy post cũ hơn mốc này.
        if (TryDecode(cursor, out var afterTicks, out var afterId))
        {
            var afterTime = new DateTimeOffset(afterTicks, TimeSpan.Zero);
            baseQuery = baseQuery.Where(p =>
                p.CreatedAt < afterTime ||
                (p.CreatedAt == afterTime && p.Id < afterId));
        }

        var rows = await baseQuery
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(limit + 1)
            .Select(p => new
            {
                p.Id,
                p.PublicId,
                p.Content,
                p.LikeCount,
                p.CommentCount,
                p.CreatedAt,
                Author = new AuthorDto(p.Author.Id, p.Author.Username, p.Author.DisplayName, p.Author.AvatarUrl),
                Media = p.Media.OrderBy(m => m.Position)
                    .Select(m => new MediaDto(m.Url, m.MediaType, m.Width, m.Height, m.Position)).ToList(),
                LikedByMe = p.Likes.Any(l => l.UserId == meId),
            })
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > limit)
        {
            var last = rows[limit - 1];
            next = Encode(last.CreatedAt, last.Id);
            rows.RemoveRange(limit, rows.Count - limit);
        }

        var items = rows.Select(p => new PostResponse(
            p.PublicId, p.Author, p.Content, p.Media,
            p.LikeCount, p.CommentCount, p.LikedByMe, p.CreatedAt)).ToList();

        return new CursorPage<PostResponse>(items, next);
    }

    private static string Encode(DateTimeOffset createdAt, long id)
        => $"{createdAt.UtcTicks}_{id}";

    private static bool TryDecode(string? cursor, out long ticks, out long id)
    {
        ticks = 0; id = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return false;
        var parts = cursor.Split('_');
        return parts.Length == 2
            && long.TryParse(parts[0], out ticks)
            && long.TryParse(parts[1], out id);
    }
}
