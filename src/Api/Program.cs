using System.Text;
using Api.Common;
using Api.Hubs;
using Application;
using Application.Common;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Infrastructure (EF Core + Redis) + Application (use case services) ----
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// User hiện tại lấy từ JWT claims.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Map lỗi nghiệp vụ (AppException) sang ProblemDetails.
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();

// ---- CORS: cho phép các frontend (Blazor giờ, React sau này) gọi API ----
// Danh sách origin đọc từ config "Cors:AllowedOrigins" -> thêm frontend mới chỉ cần thêm 1 dòng.
const string FrontendCors = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5073"];
builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));   // AllowCredentials cần cho SignalR (WebSocket) sau này

// ---- SignalR (+ Redis backplane khi chạy nhiều instance) ----
builder.Services.AddSingleton<PresenceTracker>();   // theo dõi user online (in-memory)
var signalr = builder.Services.AddSignalR();
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
{
    // BẮT BUỘC khi có >1 instance, nếu không client ở server A không nhận tin từ server B.
    signalr.AddStackExchangeRedis(redisConn);
}

// ---- Auth: JWT bearer ----
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"] ?? "dev-only-change-me-please-32chars-min"))
        };

        // SignalR gửi token qua query string ?access_token= khi mở WebSocket.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

// Chỉ ép HTTPS ngoài Development. Trong dev, frontend Blazor gọi http để tránh
// rắc rối cert tự ký + redirect 307 làm hỏng CORS/preflight.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
