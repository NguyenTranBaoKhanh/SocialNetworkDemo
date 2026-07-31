using Domain.Common;

namespace Domain.Entities;

public class PostMedia : BaseEntity
{
    public long PostId { get; set; }
    public string Url { get; set; } = default!;        // key trên S3/MinIO
    public string MediaType { get; set; } = "image";   // 'image' | 'video'
    public int? Width { get; set; }
    public int? Height { get; set; }
    public short Position { get; set; }                // thứ tự hiển thị

    public Post Post { get; set; } = default!;
}
