using Application.Common;
using Application.Posts;
using IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

public class PostServiceTests : IntegrationTestBase
{
    public PostServiceTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Create_tao_post_va_sinh_PublicId()
    {
        var alice = await SeedUserAsync("alice");

        await using var db = Fixture.CreateContext();
        var post = await Posts(db, As(alice)).CreateAsync(
            new CreatePostRequest("Xin chào", null));

        Assert.NotEqual(Guid.Empty, post.Id);   // PublicId sinh bởi DB (uuid_generate_v4)
        Assert.Equal("Xin chào", post.Content);
        Assert.Equal("alice", post.Author.Username);

        await using var check = Fixture.CreateContext();
        Assert.Equal(1, (await check.Users.SingleAsync(u => u.Id == alice)).PostCount);
    }

    [Fact]
    public async Task Create_kem_media_luu_dung_thu_tu()
    {
        var alice = await SeedUserAsync("alice");

        await using var db = Fixture.CreateContext();
        var post = await Posts(db, As(alice)).CreateAsync(new CreatePostRequest("có ảnh",
        [
            new CreateMediaDto("a.jpg", "image", 100, 100),
            new CreateMediaDto("b.jpg", "image", 200, 200),
        ]));

        Assert.Equal(2, post.Media.Count);
        Assert.Equal("a.jpg", post.Media[0].Url);
        Assert.Equal(0, post.Media[0].Position);
        Assert.Equal(1, post.Media[1].Position);
    }

    [Fact]
    public async Task Create_khong_noi_dung_khong_media_nem_ValidationException()
    {
        var alice = await SeedUserAsync("alice");

        await using var db = Fixture.CreateContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Posts(db, As(alice)).CreateAsync(new CreatePostRequest("   ", null)));
    }

    [Fact]
    public async Task Get_post_da_xoa_nem_NotFoundException()
    {
        var alice = await SeedUserAsync("alice");
        Guid postId;
        await using (var db = Fixture.CreateContext())
            postId = (await Posts(db, As(alice)).CreateAsync(new CreatePostRequest("x", null))).Id;

        await using (var db = Fixture.CreateContext())
            await Posts(db, As(alice)).DeleteAsync(postId);   // soft delete

        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Posts(db2, As(alice)).GetByPublicIdAsync(postId));
    }

    [Fact]
    public async Task Xoa_post_nguoi_khac_nem_ForbiddenException()
    {
        var alice = await SeedUserAsync("alice");
        var bob = await SeedUserAsync("bob");

        Guid postId;
        await using (var db = Fixture.CreateContext())
            postId = (await Posts(db, As(alice)).CreateAsync(new CreatePostRequest("của alice", null))).Id;

        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Posts(db2, As(bob)).DeleteAsync(postId));
    }

    [Fact]
    public async Task Xoa_la_soft_delete_ban_ghi_van_con_trong_DB()
    {
        var alice = await SeedUserAsync("alice");
        Guid postId;
        await using (var db = Fixture.CreateContext())
            postId = (await Posts(db, As(alice)).CreateAsync(new CreatePostRequest("x", null))).Id;

        await using (var db = Fixture.CreateContext())
            await Posts(db, As(alice)).DeleteAsync(postId);

        await using var check = Fixture.CreateContext();
        var row = await check.Posts.IgnoreQueryFilters().SingleAsync(p => p.PublicId == postId);
        Assert.NotNull(row.DeletedAt);   // vẫn còn hàng, chỉ set DeletedAt
    }
}
