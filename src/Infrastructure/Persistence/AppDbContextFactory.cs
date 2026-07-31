using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

/// <summary>
/// Design-time factory để `dotnet ef migrations` chạy được mà không cần khởi động Api.
/// Connection string ở đây chỉ dùng lúc tạo migration (không dùng lúc runtime).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=socialdemo;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
