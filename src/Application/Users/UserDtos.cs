namespace Application.Users;

/// <summary>Profile user hiện tại (cho sidebar).</summary>
public record UserProfileResponse(
    long Id,
    string Username,
    string DisplayName,
    string Bio,
    string? AvatarUrl,
    int FollowerCount,
    int FollowingCount,
    int PostCount);

/// <summary>Profile khi xem 1 user (kèm cờ quan hệ với người xem).</summary>
public record UserProfileView(
    long Id,
    string Username,
    string DisplayName,
    string Bio,
    string? AvatarUrl,
    int FollowerCount,
    int FollowingCount,
    int PostCount,
    bool IsMe,
    bool IsFollowedByMe);

/// <summary>Thông tin rút gọn (gợi ý follow).</summary>
public record UserSummary(long Id, string Username, string DisplayName, string? AvatarUrl);

public record UpdateAvatarRequest(string Url);

public record UpdateProfileRequest(string DisplayName, string Bio);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
