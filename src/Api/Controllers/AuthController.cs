using Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req, CancellationToken ct)
        => Ok(await _auth.RegisterAsync(req, ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req, CancellationToken ct)
        => Ok(await _auth.LoginAsync(req, ct));

    /// <summary>Đổi refresh token lấy cặp access + refresh token mới.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(req, ct));

    /// <summary>Thu hồi refresh token (đăng xuất).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req, ct);
        return NoContent();
    }
}
