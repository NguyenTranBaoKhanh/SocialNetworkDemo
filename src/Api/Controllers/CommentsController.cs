using Application.Comments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/comments")]
public class CommentsController : ControllerBase
{
    private readonly CommentService _comments;

    public CommentsController(CommentService comments) => _comments = comments;

    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _comments.DeleteAsync(id, ct);
        return NoContent();
    }
}
