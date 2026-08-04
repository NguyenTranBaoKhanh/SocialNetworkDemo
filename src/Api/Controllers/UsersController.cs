using Application.Feed;
using Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly UserService _users;
    private readonly FeedService _feed;

    public UsersController(UserService users, FeedService feed)
    {
        _users = users;
        _feed = feed;
    }

    /// <summary>Thông tin user đang đăng nhập.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> Me(CancellationToken ct)
        => Ok(await _users.GetMeAsync(ct));

    /// <summary>Cập nhật tên hiển thị + bio.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest req, CancellationToken ct)
    {
        await _users.UpdateProfileAsync(req, ct);
        return NoContent();
    }

    /// <summary>Đổi avatar (url ảnh đã upload qua /api/media).</summary>
    [HttpPut("me/avatar")]
    public async Task<IActionResult> UpdateAvatar(UpdateAvatarRequest req, CancellationToken ct)
    {
        await _users.UpdateAvatarAsync(req, ct);
        return NoContent();
    }

    /// <summary>Đổi mật khẩu (xác minh mật khẩu cũ; thu hồi refresh token).</summary>
    [HttpPost("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        await _users.ChangePasswordAsync(req, ct);
        return NoContent();
    }

    /// <summary>Gợi ý follow.</summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> Suggestions([FromQuery] int limit = 5, CancellationToken ct = default)
        => Ok(await _users.GetSuggestionsAsync(limit, ct));

    /// <summary>Profile 1 user theo username.</summary>
    [HttpGet("{username}")]
    public async Task<ActionResult<UserProfileView>> Profile(string username, CancellationToken ct)
        => Ok(await _users.GetProfileAsync(username, ct));

    /// <summary>Các bài của 1 user (cursor pagination).</summary>
    [HttpGet("{username}/posts")]
    public async Task<IActionResult> Posts(
        string username, [FromQuery] string? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _feed.GetUserPostsAsync(username, cursor, limit, ct));
}
