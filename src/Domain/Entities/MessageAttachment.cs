using Domain.Common;

namespace Domain.Entities;

public class MessageAttachment : BaseEntity
{
    public long MessageId { get; set; }
    public string Url { get; set; } = default!;        // key trên S3/MinIO
    public string MediaType { get; set; } = "image";   // 'image' | 'video' | 'file'
    public string? FileName { get; set; }
    public long? FileSize { get; set; }

    public Message Message { get; set; } = default!;
}
