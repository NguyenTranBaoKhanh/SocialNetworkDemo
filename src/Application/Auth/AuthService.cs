using Application.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Auth;

public class AuthService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public AuthService(IAppDbContext db, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        var username = (req.Username ?? "").Trim();
        var email = (req.Email ?? "").Trim();

        if (username.Length < 3)
            throw new ValidationException("Username tối thiểu 3 ký tự.");
        if (!email.Contains('@'))
            throw new ValidationException("Email không hợp lệ.");
        if ((req.Password ?? "").Length < 6)
            throw new ValidationException("Mật khẩu tối thiểu 6 ký tự.");
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            throw new ValidationException("DisplayName không được trống.");

        // citext -> so sánh không phân biệt hoa thường; vẫn có unique constraint chốt ở DB.
        if (await _db.Users.AnyAsync(u => u.Username == username, ct))
            throw new ConflictException("Username đã tồn tại.");
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Email đã được dùng.");

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = req.DisplayName.Trim(),
            PasswordHash = _hasher.Hash(req.Password!),
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);   // lấy user.Id

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var key = (req.UsernameOrEmail ?? "").Trim();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == key || u.Email == key, ct);

        // Cùng một thông báo cho "không tồn tại" và "sai mật khẩu" (tránh lộ user tồn tại).
        if (user is null || !_hasher.Verify(user.PasswordHash, req.Password ?? ""))
            throw new UnauthorizedException("Sai thông tin đăng nhập.");
        if (!user.IsActive)
            throw new ForbiddenException("Tài khoản đã bị khóa.");

        return await IssueTokensAsync(user, ct);
    }

    /// <summary>Đổi refresh token lấy cặp token mới (xoay vòng — revoke token cũ).</summary>
    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, CancellationToken ct = default)
    {
        var hash = _jwt.HashRefreshToken((req.RefreshToken ?? "").Trim());

        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            throw new UnauthorizedException("Refresh token không hợp lệ.");

        // Token đã revoke mà vẫn bị dùng -> nghi bị đánh cắp: thu hồi TẤT CẢ token của user.
        if (stored.RevokedAt is not null)
        {
            await RevokeAllForUserAsync(stored.UserId, ct);
            throw new UnauthorizedException("Refresh token đã bị thu hồi.");
        }

        if (DateTimeOffset.UtcNow >= stored.ExpiresAt)
            throw new UnauthorizedException("Refresh token đã hết hạn.");

        // Xoay vòng: revoke cũ, cấp mới.
        var newRefresh = _jwt.GenerateRefreshToken();
        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.ReplacedByTokenHash = newRefresh.TokenHash;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newRefresh.TokenHash,
            ExpiresAt = newRefresh.ExpiresAt,
        });

        var (access, expiresAt) = _jwt.GenerateAccessToken(stored.User);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(access, expiresAt, newRefresh.RawToken,
            stored.User.Id, stored.User.Username, stored.User.DisplayName);
    }

    /// <summary>Thu hồi refresh token (đăng xuất). Idempotent.</summary>
    public async Task LogoutAsync(LogoutRequest req, CancellationToken ct = default)
    {
        var hash = _jwt.HashRefreshToken((req.RefreshToken ?? "").Trim());
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (access, expiresAt) = _jwt.GenerateAccessToken(user);
        var refresh = _jwt.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.TokenHash,
            ExpiresAt = refresh.ExpiresAt,
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(access, expiresAt, refresh.RawToken,
            user.Id, user.Username, user.DisplayName);
    }

    private async Task RevokeAllForUserAsync(long userId, CancellationToken ct)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active)
            t.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
