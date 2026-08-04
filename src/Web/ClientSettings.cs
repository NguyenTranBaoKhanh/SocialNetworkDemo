namespace Web;

/// <summary>Cấu hình client dùng chung cho component (ví dụ ghép URL ảnh tuyệt đối).</summary>
public class ClientSettings
{
    public string ApiBaseUrl { get; init; } = "";

    /// <summary>Ghép url media tương đối ("/api/media/...") thành URL tuyệt đối để render &lt;img&gt;.</summary>
    public string MediaUrl(string relativeUrl) => $"{ApiBaseUrl}{relativeUrl}";
}
