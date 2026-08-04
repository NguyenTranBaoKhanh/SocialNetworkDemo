using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Web.Models;

namespace Web.Services;

/// <summary>Upload ảnh/video lên API (multipart). Dùng client có Bearer.</summary>
public class MediaApi
{
    private const long MaxBytes = 50 * 1024 * 1024;   // đủ cho video 50MB (server validate theo loại)
    private readonly HttpClient _http;

    public MediaApi(IHttpClientFactory factory) => _http = factory.CreateClient("AuthorizedApi");

    /// <summary>Upload 1 file (ảnh hoặc video), trả về CreateMediaDto (url + mediaType) để đính vào post.</summary>
    public async Task<CreateMediaDto?> UploadAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream(MaxBytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);

        var res = await _http.PostAsync("api/media", content);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(await ReadDetailAsync(res));

        var dto = await res.Content.ReadFromJsonAsync<UploadResponse>();
        return dto is null ? null : new CreateMediaDto(dto.Url, dto.MediaType, null, null);
    }

    /// <summary>Đọc thông báo lỗi thật ("detail") từ response để hiện cho người dùng.</summary>
    private static async Task<string> ReadDetailAsync(HttpResponseMessage res)
    {
        try
        {
            var err = await res.Content.ReadFromJsonAsync<ErrorBody>();
            if (!string.IsNullOrWhiteSpace(err?.Detail)) return err!.Detail!;
        }
        catch { /* body không phải JSON */ }
        return $"Tải lên thất bại ({(int)res.StatusCode}).";
    }

    private record UploadResponse(string Url, string MediaType);
    private record ErrorBody(string? Detail);
}
