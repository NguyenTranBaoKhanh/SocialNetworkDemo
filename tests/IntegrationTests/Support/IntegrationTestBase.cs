using Application.Auth;
using Application.Comments;
using Application.Common;
using Application.Feed;
using Application.Follows;
using Application.Likes;
using Application.Posts;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Support;

/// <summary>
/// Base cho mọi test class: reset DB trước mỗi test (độc lập), và cung cấp factory
/// tạo service Application gắn với DbContext + user hiện tại tùy test.
/// </summary>
[Collection(DatabaseCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly DatabaseFixture Fixture;

    protected IntegrationTestBase(DatabaseFixture fixture) => Fixture = fixture;

    // Reset chạy trước MỖI test method (xUnit tạo instance mới cho từng test).
    public Task InitializeAsync() => Fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---- Hạ tầng security dùng thật (không mock) ----
    private static readonly IPasswordHasher Hasher = new PasswordHasherAdapter();

    private static readonly IJwtTokenGenerator Jwt = new JwtTokenGenerator(
        Options.Create(new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            Key = "test-signing-key-at-least-32-characters-long",
            ExpiryMinutes = 60,
        }));

    // ---- Factory service: mỗi cái nhận context + user để test kiểm soát rõ ràng ----
    protected static AuthService Auth(AppDbContext db) => new(db, Hasher, Jwt);
    protected static PostService Posts(AppDbContext db, ICurrentUser u) => new(db, u);
    protected static CommentService Comments(AppDbContext db, ICurrentUser u) => new(db, u);
    protected static LikeService Likes(AppDbContext db, ICurrentUser u) => new(db, u);
    protected static FollowService Follows(AppDbContext db, ICurrentUser u) => new(db, u);
    protected static FeedService Feed(AppDbContext db, ICurrentUser u) => new(db, u);

    protected static TestCurrentUser As(long userId) => new() { Id = userId };

    /// <summary>Tạo nhanh 1 user qua AuthService thật, trả về userId. Dùng để seed.</summary>
    protected async Task<long> SeedUserAsync(string username)
    {
        await using var db = Fixture.CreateContext();
        var res = await Auth(db).RegisterAsync(
            new RegisterRequest(username, $"{username}@test.com", "secret123", username));
        return res.UserId;
    }
}
