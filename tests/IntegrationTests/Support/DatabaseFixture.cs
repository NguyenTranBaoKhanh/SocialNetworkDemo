using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Support;

/// <summary>
/// Khởi động MỘT Postgres thật (Testcontainers) dùng chung cho cả test collection.
/// Apply migration khi start; cung cấp cách tạo DbContext và reset dữ liệu giữa các test.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("socialdemo_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply toàn bộ migration -> tạo bảng, extension (uuid-ossp/citext), constraint, index.
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Tạo AppDbContext mới trỏ vào container test.</summary>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Xóa sạch dữ liệu, reset identity — gọi trước mỗi test để độc lập.</summary>
    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE
              message_attachments, messages, conversation_members, conversations,
              post_media, comments, likes, follows, posts, users
            RESTART IDENTITY CASCADE;
            """);
    }
}

/// <summary>Định danh collection để xUnit chia sẻ 1 container cho mọi test class.</summary>
[CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
