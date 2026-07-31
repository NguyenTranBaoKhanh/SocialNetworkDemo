using Application.Follows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users/{username}/follow")]
public class FollowsController : ControllerBase
{
    private readonly FollowService _follows;

    public FollowsController(FollowService follows) => _follows = follows;

    [HttpPost]
    public async Task<ActionResult<FollowResult>> Follow(string username, CancellationToken ct)
        => Ok(await _follows.FollowAsync(username, ct));

    [HttpDelete]
    public async Task<ActionResult<FollowResult>> Unfollow(string username, CancellationToken ct)
        => Ok(await _follows.UnfollowAsync(username, ct));
}
