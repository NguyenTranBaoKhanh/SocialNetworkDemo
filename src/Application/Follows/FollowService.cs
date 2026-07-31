using Application.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Follows;

public record FollowResult(bool Following);

public class FollowService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;

    public FollowService(IAppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>Follow user theo username. Idempotent, chống tự-follow.</summary>
    public async Task<FollowResult> FollowAsync(string username, CancellationToken ct = default)
    {
        var meId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var target = await _db.Users.FirstOrDefaultAsync(
            u => u.Username == username.Trim() && u.IsActive, ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        if (target.Id == meId)
            throw new ValidationException("Không thể tự follow chính mình.");

        var exists = await _db.Follows.AnyAsync(
            f => f.FollowerId == meId && f.FolloweeId == target.Id, ct);
        if (!exists)
        {
            _db.Follows.Add(new Follow { FollowerId = meId, FolloweeId = target.Id });

            var me = await _db.Users.FirstAsync(u => u.Id == meId, ct);
            me.FollowingCount++;
            target.FollowerCount++;   // counter cache

            await _db.SaveChangesAsync(ct);
        }

        return new FollowResult(true);
    }

    public async Task<FollowResult> UnfollowAsync(string username, CancellationToken ct = default)
    {
        var meId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim(), ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        var follow = await _db.Follows.FirstOrDefaultAsync(
            f => f.FollowerId == meId && f.FolloweeId == target.Id, ct);
        if (follow is not null)
        {
            _db.Follows.Remove(follow);

            var me = await _db.Users.FirstAsync(u => u.Id == meId, ct);
            if (me.FollowingCount > 0) me.FollowingCount--;
            if (target.FollowerCount > 0) target.FollowerCount--;

            await _db.SaveChangesAsync(ct);
        }

        return new FollowResult(false);
    }
}
