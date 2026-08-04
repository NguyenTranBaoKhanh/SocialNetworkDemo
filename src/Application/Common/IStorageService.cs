namespace Application.Common;

/// <summary>Kết quả đọc object: nội dung + content type để trả cho client.</summary>
public record StoredObject(Stream Content, string ContentType);

/// <summary>
/// Lưu trữ file media (ảnh/video). Hiện thực bằng MinIO/S3 ở Infrastructure.
/// Application chỉ biết interface này -> đổi backend lưu trữ không ảnh hưởng use case.
/// </summary>
public interface IStorageService
{
    /// <summary>Upload nội dung, trả về object key (dùng để đọc lại sau).</summary>
    Task<string> UploadAsync(Stream content, string contentType, string extension, CancellationToken ct = default);

    /// <summary>Đọc object theo key; null nếu không tồn tại.</summary>
    Task<StoredObject?> GetAsync(string objectKey, CancellationToken ct = default);
}
