using Application.Feed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/feed")]
public class FeedController : ControllerBase
{
    private readonly FeedService _feed;

    public FeedController(FeedService feed) => _feed = feed;

    /// <summary>Feed của user hiện tại. Truyền ?cursor= để lấy trang kế.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _feed.GetAsync(cursor, limit, ct));
}
