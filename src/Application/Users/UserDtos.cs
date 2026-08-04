namespace Application.Users;

public record UserProfileResponse(
    long Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    int FollowerCount,
    int FollowingCount,
    int PostCount);
