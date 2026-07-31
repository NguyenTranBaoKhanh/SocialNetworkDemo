namespace Application.Common;

/// <summary>Thông tin tác giả rút gọn, nhúng vào post/comment.</summary>
public record AuthorDto(long Id, string Username, string DisplayName, string? AvatarUrl);

/// <summary>Trang kết quả dùng cursor pagination (cursor = mốc để lấy trang kế).</summary>
public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
