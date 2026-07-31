namespace Domain.Common;

/// <summary>
/// Base cho entity dùng bigint identity làm khóa chính.
/// Thời gian do server quyết định (không tin client) — đặt mặc định ở tầng DB.
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
