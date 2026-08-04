using Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private const long MaxBytes = 5 * 1024 * 1024;   // 5 MB
    private static readonly Dictionary<string, string> AllowedImages = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    private readonly IStorageService _storage;

    public MediaController(IStorageService storage) => _storage = storage;

    /// <summary>Upload 1 ảnh (multipart form-data, field "file"). Trả về url tương đối để render.</summary>
    [Authorize]
    [HttpPost]
    [RequestSizeLimit(MaxBytes + 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "Chưa chọn file." });
        if (file.Length > MaxBytes)
            return BadRequest(new { detail = "Ảnh tối đa 5MB." });
        if (!AllowedImages.TryGetValue(file.ContentType, out var ext))
            return BadRequest(new { detail = "Chỉ chấp nhận ảnh JP/PNG/WEBP/GIF." });

        await using var stream = file.OpenReadStream();
        var key = await _storage.UploadAsync(stream, file.ContentType, ext, ct);

        // URL tương đối; frontend ghép với ApiBaseUrl để render <img>.
        return Ok(new { url = $"/api/media/{key}", mediaType = "image" });
    }

    /// <summary>Phục vụ ảnh (public, để thẻ &lt;img&gt; tải được — không cần Bearer).</summary>
    [AllowAnonymous]
    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var obj = await _storage.GetAsync(key, ct);
        if (obj is null) return NotFound();
        return File(obj.Content, obj.ContentType);
    }
}
