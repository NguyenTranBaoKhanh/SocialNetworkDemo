using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Profile mạng xã hội. Auth (password hash, login) có thể do ASP.NET Core Identity
/// quản lý; ở đây giữ dữ liệu profile + counter cache.
/// </summary>
public class User : BaseEntity
{
    public Guid PublicId { get; set; }
    public string Username { get; set; } = default!;   // @handle, duy nhất
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Bio { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string PasswordHash { get; set; } = default!;

    // Counter cache — nguồn sự thật là bảng follows/posts. Flush từ Redis/worker.
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int PostCount { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Follow> Following { get; set; } = new List<Follow>();  // mình đi follow
    public ICollection<Follow> Followers { get; set; } = new List<Follow>();  // ai follow mình
}
