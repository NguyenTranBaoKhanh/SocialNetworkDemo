using Application.Likes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/posts/{id:guid}/like")]
public class LikesController : ControllerBase
{
    private readonly LikeService _likes;

    public LikesController(LikeService likes) => _likes = likes;

    [HttpPost]
    public async Task<ActionResult<LikeResult>> Like(Guid id, CancellationToken ct)
        => Ok(await _likes.LikeAsync(id, ct));

    [HttpDelete]
    public async Task<ActionResult<LikeResult>> Unlike(Guid id, CancellationToken ct)
        => Ok(await _likes.UnlikeAsync(id, ct));
}
