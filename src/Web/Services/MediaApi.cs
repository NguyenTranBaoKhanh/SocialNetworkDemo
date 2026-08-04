using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Web.Models;

namespace Web.Services;

/// <summary>Upload ảnh lên API (multipart). Dùng client có Bearer.</summary>
public class MediaApi
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private readonly HttpClient _http;

    public MediaApi(IHttpClientFactory factory) => _http = factory.CreateClient("AuthorizedApi");

    /// <summary>Upload 1 ảnh, trả về CreateMediaDto (url tương đối) để đính vào post.</summary>
    public async Task<CreateMediaDto?> UploadImageAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream(MaxBytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);

        var res = await _http.PostAsync("api/media", content);
        res.EnsureSuccessStatusCode();

        var dto = await res.Content.ReadFromJsonAsync<UploadResponse>();
        return dto is null ? null : new CreateMediaDto(dto.Url, dto.MediaType, null, null);
    }

    private record UploadResponse(string Url, string MediaType);
}
