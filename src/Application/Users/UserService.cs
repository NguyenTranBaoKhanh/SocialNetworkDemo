using Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Application.Users;

public class UserService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _current;
    private readonly IPasswordHasher _hasher;

    public UserService(IAppDbContext db, ICurrentUser current, IPasswordHasher hasher)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
    }

    /// <summary>Thông tin user đang đăng nhập (cho sidebar).</summary>
    public async Task<UserProfileResponse> GetMeAsync(CancellationToken ct = default)
    {
        var id = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var u = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        return new UserProfileResponse(
            u.Id, u.Username, u.DisplayName, u.Bio, u.AvatarUrl,
            u.FollowerCount, u.FollowingCount, u.PostCount);
    }

    /// <summary>Profile 1 user theo username, kèm quan hệ với người đang xem.</summary>
    public async Task<UserProfileView> GetProfileAsync(string username, CancellationToken ct = default)
    {
        var meId = _current.Id;

        var u = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username == username.Trim(), ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        var isFollowed = meId != null &&
            await _db.Follows.AnyAsync(f => f.FollowerId == meId && f.FolloweeId == u.Id, ct);

        return new UserProfileView(
            u.Id, u.Username, u.DisplayName, u.Bio, u.AvatarUrl,
            u.FollowerCount, u.FollowingCount, u.PostCount,
            IsMe: meId == u.Id, IsFollowedByMe: isFollowed);
    }

    /// <summary>Cập nhật tên hiển thị + bio của user hiện tại.</summary>
    public async Task UpdateProfileAsync(UpdateProfileRequest req, CancellationToken ct = default)
    {
        var id = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        var displayName = (req.DisplayName ?? "").Trim();
        if (displayName.Length == 0)
            throw new ValidationException("Tên hiển thị không được trống.");
        if (displayName.Length > 100)
            throw new ValidationException("Tên hiển thị tối đa 100 ký tự.");
        var bio = (req.Bio ?? "").Trim();
        if (bio.Length > 500)
            throw new ValidationException("Bio tối đa 500 ký tự.");

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        u.DisplayName = displayName;
        u.Bio = bio;
        u.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Cập nhật avatar của user hiện tại (url media đã upload).</summary>
    public async Task UpdateAvatarAsync(UpdateAvatarRequest req, CancellationToken ct = default)
    {
        var id = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");
        if (string.IsNullOrWhiteSpace(req.Url))
            throw new ValidationException("Url avatar không hợp lệ.");

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        u.AvatarUrl = req.Url;
        u.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Đổi mật khẩu: xác minh mật khẩu cũ, đặt mật khẩu mới, thu hồi toàn bộ refresh token.</summary>
    public async Task ChangePasswordAsync(ChangePasswordRequest req, CancellationToken ct = default)
    {
        var id = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");

        if ((req.NewPassword ?? "").Length < 6)
            throw new ValidationException("Mật khẩu mới tối thiểu 6 ký tự.");

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy user.");

        if (!_hasher.Verify(u.PasswordHash, req.CurrentPassword ?? ""))
            throw new ValidationException("Mật khẩu hiện tại không đúng.");

        u.PasswordHash = _hasher.Hash(req.NewPassword!);
        u.UpdatedAt = DateTimeOffset.UtcNow;

        // Thu hồi mọi refresh token đang hoạt động (buộc đăng nhập lại ở các thiết bị khác).
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(ct);
        foreach (var t in active) t.RevokedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Gợi ý follow: user chưa được mình follow, không phải mình, nhiều follower trước.</summary>
    public async Task<IReadOnlyList<UserSummary>> GetSuggestionsAsync(int limit = 5, CancellationToken ct = default)
    {
        var meId = _current.Id ?? throw new UnauthorizedException("Cần đăng nhập.");
        limit = Math.Clamp(limit, 1, 20);

        var followedIds = _db.Follows.Where(f => f.FollowerId == meId).Select(f => f.FolloweeId);

        return await _db.Users.AsNoTracking()
            .Where(u => u.Id != meId && u.IsActive && !followedIds.Contains(u.Id))
            .OrderByDescending(u => u.FollowerCount).ThenByDescending(u => u.Id)
            .Take(limit)
            .Select(u => new UserSummary(u.Id, u.Username, u.DisplayName, u.AvatarUrl))
            .ToListAsync(ct);
    }
}
