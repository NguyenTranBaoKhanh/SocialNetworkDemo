using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;    // ảnh ≤ 5MB
    private const long MaxVideoBytes = 50 * 1024 * 1024;   // video ≤ 50MB

    // content-type -> (đuôi file, loại media)
    private static readonly Dictionary<string, (string Ext, string Type)> Allowed = new()
    {
        ["image/jpeg"] = (".jpg", "image"),
        ["image/png"] = (".png", "image"),
        ["image/webp"] = (".webp", "image"),
        ["image/gif"] = (".gif", "image"),
        ["video/mp4"] = (".mp4", "video"),
        ["video/webm"] = (".webm", "video"),
        ["video/quicktime"] = (".mov", "video"),
    };

    private readonly IStorageService _storage;

    public MediaController(IStorageService storage) => _storage = storage;

    /// <summary>Upload 1 ảnh hoặc video (multipart, field "file"). Trả về url tương đối + loại.</summary>
    [Authorize]
    [HttpPost]
    [RequestSizeLimit(MaxVideoBytes + 4096)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "Chưa chọn file." });
        if (!Allowed.TryGetValue(file.ContentType, out var kind))
            return BadRequest(new { detail = $"Định dạng '{file.ContentType}' không hỗ trợ. Chỉ nhận ảnh (JPG/PNG/WEBP/GIF) hoặc video (MP4/WEBM/MOV)." });

        var max = kind.Type == "video" ? MaxVideoBytes : MaxImageBytes;
        if (file.Length > max)
            return BadRequest(new { detail = $"{(kind.Type == "video" ? "Video" : "Ảnh")} tối đa {max / (1024 * 1024)}MB." });

        await using var stream = file.OpenReadStream();
        var key = await _storage.UploadAsync(stream, file.ContentType, kind.Ext, ct);

        // URL tương đối; frontend ghép với ApiBaseUrl để render <img>/<video>.
        return Ok(new { url = $"/api/media/{key}", mediaType = kind.Type });
    }

    /// <summary>Phục vụ media (public, để thẻ &lt;img&gt;/&lt;video&gt; tải được — không cần Bearer).</summary>
    [AllowAnonymous]
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var obj = await _storage.GetAsync(key, ct);
        if (obj is null) return NotFound();
        return File(obj.Content, obj.ContentType);
    }
}
