using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Web;
using Web.Auth;
using Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Địa chỉ API backend. Chọn theo scheme của chính trang để tránh mixed-content:
//  - Mở app bằng http  -> gọi API http  (http://localhost:5273)
//  - Mở app bằng https -> gọi API https (https://localhost:7068)
var isHttps = builder.HostEnvironment.BaseAddress
    .StartsWith("https", StringComparison.OrdinalIgnoreCase);
var apiBase = isHttps
    ? (builder.Configuration["ApiBaseUrlHttps"] ?? "https://localhost:7068")
    : (builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5273");

// Lưu token + trạng thái đăng nhập.
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

// Handler tự gắn Bearer + refresh khi 401.
builder.Services.AddScoped<AuthorizedHandler>();

// "Api": client thuần (login/register/refresh). "AuthorizedApi": có Bearer + auto-refresh.
builder.Services.AddHttpClient("Api", c => c.BaseAddress = new Uri(apiBase));
builder.Services.AddHttpClient("AuthorizedApi", c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<AuthorizedHandler>();

// Service gọi API.
builder.Services.AddScoped<AuthApi>();
builder.Services.AddScoped<PostApi>();

await builder.Build().RunAsync();
