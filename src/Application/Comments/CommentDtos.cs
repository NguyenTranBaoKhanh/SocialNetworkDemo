using Application.Common;

namespace Application.Comments;

public record CreateCommentRequest(string Content, long? ParentId);

public record CommentResponse(
    long Id,
    long? ParentId,
    AuthorDto Author,
    string Content,
    int LikeCount,
    DateTimeOffset CreatedAt);
