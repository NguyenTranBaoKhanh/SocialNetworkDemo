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
        await _db.SaveChangesAsync(ct);

        return BuildResponse(user);
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

        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = _jwt.Generate(user);
        return new AuthResponse(token, expiresAt, user.Id, user.Username, user.DisplayName);
    }
}
