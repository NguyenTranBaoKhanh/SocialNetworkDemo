using Application.Common;
using IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

public class FollowServiceTests : IntegrationTestBase
{
    public FollowServiceTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Follow_tao_quan_he_va_tang_counter()
    {
        var alice = await SeedUserAsync("alice");
        await SeedUserAsync("bob");

        await using var db = Fixture.CreateContext();
        var res = await Follows(db, As(alice)).FollowAsync("bob");
        Assert.True(res.Following);

        await using var check = Fixture.CreateContext();
        Assert.Equal(1, await check.Follows.CountAsync());
        var aliceRow = await check.Users.SingleAsync(u => u.Id == alice);
        var bobRow = await check.Users.SingleAsync(u => u.Username == "bob");
        Assert.Equal(1, aliceRow.FollowingCount);
        Assert.Equal(1, bobRow.FollowerCount);
    }

    [Fact]
    public async Task Follow_hai_lan_khong_tao_ban_ghi_dup()
    {
        var alice = await SeedUserAsync("alice");
        await SeedUserAsync("bob");

        await using (var db = Fixture.CreateContext())
            await Follows(db, As(alice)).FollowAsync("bob");
        await using (var db = Fixture.CreateContext())
            await Follows(db, As(alice)).FollowAsync("bob");   // idempotent

        await using var check = Fixture.CreateContext();
        Assert.Equal(1, await check.Follows.CountAsync());
        Assert.Equal(1, (await check.Users.SingleAsync(u => u.Id == alice)).FollowingCount);
    }

    [Fact]
    public async Task Tu_follow_chinh_minh_nem_ValidationException()
    {
        var alice = await SeedUserAsync("alice");

        await using var db = Fixture.CreateContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Follows(db, As(alice)).FollowAsync("alice"));
    }

    [Fact]
    public async Task Follow_user_khong_ton_tai_nem_NotFoundException()
    {
        var alice = await SeedUserAsync("alice");

        await using var db = Fixture.CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Follows(db, As(alice)).FollowAsync("ghost"));
    }

    [Fact]
    public async Task Unfollow_xoa_quan_he_va_giam_counter()
    {
        var alice = await SeedUserAsync("alice");
        await SeedUserAsync("bob");

        await using (var db = Fixture.CreateContext())
            await Follows(db, As(alice)).FollowAsync("bob");
        await using (var db = Fixture.CreateContext())
            await Follows(db, As(alice)).UnfollowAsync("bob");

        await using var check = Fixture.CreateContext();
        Assert.Equal(0, await check.Follows.CountAsync());
        Assert.Equal(0, (await check.Users.SingleAsync(u => u.Id == alice)).FollowingCount);
        Assert.Equal(0, (await check.Users.SingleAsync(u => u.Username == "bob")).FollowerCount);
    }
}
