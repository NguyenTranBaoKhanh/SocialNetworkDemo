using Application.Comments;
using Application.Posts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/posts")]
public class PostsController : ControllerBase
{
    private readonly PostService _posts;
    private readonly CommentService _comments;

    public PostsController(PostService posts, CommentService comments)
    {
        _posts = posts;
        _comments = comments;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PostResponse>> Create(CreatePostRequest req, CancellationToken ct)
    {
        var post = await _posts.CreateAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = post.Id }, post);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _posts.GetByPublicIdAsync(id, ct));

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _posts.DeleteAsync(id, ct);
        return NoContent();
    }

    // -------- Comments của một post --------

    [Authorize]
    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<CommentResponse>> AddComment(
        Guid id, CreateCommentRequest req, CancellationToken ct)
        => Ok(await _comments.AddAsync(id, req, ct));

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> ListComments(
        Guid id, [FromQuery] long? after, [FromQuery] int limit = 20, CancellationToken ct = default)
        => Ok(await _comments.ListAsync(id, after, limit, ct));
}
