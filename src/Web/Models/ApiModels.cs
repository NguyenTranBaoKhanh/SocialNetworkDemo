namespace Web.Models;

// ---- Request gửi lên API ----
public record RegisterRequest(string Username, string Email, string Password, string DisplayName);
public record LoginRequest(string UsernameOrEmail, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);
public record CreatePostRequest(string Content, List<CreateMediaDto>? Media);
public record CreateMediaDto(string Url, string MediaType, int? Width, int? Height);
public record CreateCommentRequest(string Content, long? ParentId);

// ---- Response nhận từ API (khớp DTO backend) ----
public record AuthResponse(
    string Token, DateTimeOffset ExpiresAt, string RefreshToken,
    long UserId, string Username, string DisplayName);

public record AuthorDto(long Id, string Username, string DisplayName, string? AvatarUrl);

public record UserProfile(
    long Id, string Username, string DisplayName, string Bio, string? AvatarUrl,
    int FollowerCount, int FollowingCount, int PostCount);

public record UserProfileView(
    long Id, string Username, string DisplayName, string Bio, string? AvatarUrl,
    int FollowerCount, int FollowingCount, int PostCount,
    bool IsMe, bool IsFollowedByMe);

public record UserSummary(long Id, string Username, string DisplayName, string? AvatarUrl);

public record UpdateAvatarRequest(string Url);
public record UpdateProfileRequest(string DisplayName, string Bio);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record MediaDto(string Url, string MediaType, int? Width, int? Height, short Position);

public record PostResponse(
    Guid Id, AuthorDto Author, string Content, List<MediaDto> Media,
    int LikeCount, int CommentCount, bool LikedByMe, DateTimeOffset CreatedAt);

public record CommentResponse(
    long Id, long? ParentId, AuthorDto Author, string Content, int LikeCount, DateTimeOffset CreatedAt);

public record LikeResult(int LikeCount, bool LikedByMe);

public record FollowResult(bool Following);

public record CursorPage<T>(List<T> Items, string? NextCursor);
