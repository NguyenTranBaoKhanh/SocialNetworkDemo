using Application.Common;

namespace Application.Posts;

public record CreatePostRequest(string Content, List<CreateMediaDto>? Media);

public record CreateMediaDto(string Url, string MediaType, int? Width, int? Height);

public record MediaDto(string Url, string MediaType, int? Width, int? Height, short Position);

public record PostResponse(
    Guid Id,
    AuthorDto Author,
    string Content,
    IReadOnlyList<MediaDto> Media,
    int LikeCount,
    int CommentCount,
    bool LikedByMe,
    DateTimeOffset CreatedAt);
