using Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.Users;

public class UserService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;

    public UserService(IAppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>Thông tin user đang đăng nhập (cho sidebar/profile).</summary>
    public async Task<UserProfileResponse> GetMeAsync(CancellationToken ct = default)
    {
        var id = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var u = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        return new UserProfileResponse(
            u.Id, u.Username, u.DisplayName, u.AvatarUrl,
            u.FollowerCount, u.FollowingCount, u.PostCount);
    }
}
