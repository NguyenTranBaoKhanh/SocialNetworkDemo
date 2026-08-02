using Application.Posts;
using IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

public class LikeServiceTests : IntegrationTestBase
{
    public LikeServiceTests(DatabaseFixture fixture) : base(fixture) { }

    private async Task<Guid> SeedPostAsync(long authorId, string content = "hello")
    {
        await using var db = Fixture.CreateContext();
        var post = await Posts(db, As(authorId)).CreateAsync(new CreatePostRequest(content, null));
        return post.Id;
    }

    [Fact]
    public async Task Like_tang_count_va_danh_dau_likedByMe()
    {
        var bob = await SeedUserAsync("bob");
        var alice = await SeedUserAsync("alice");
        var postId = await SeedPostAsync(bob);

        await using var db = Fixture.CreateContext();
        var res = await Likes(db, As(alice)).LikeAsync(postId);

        Assert.Equal(1, res.LikeCount);
        Assert.True(res.LikedByMe);

        await using var check = Fixture.CreateContext();
        Assert.Equal(1, await check.Likes.CountAsync());
    }

    [Fact]
    public async Task Like_hai_lan_khong_tang_dup()
    {
        var bob = await SeedUserAsync("bob");
        var alice = await SeedUserAsync("alice");
        var postId = await SeedPostAsync(bob);

        await using (var db = Fixture.CreateContext())
            await Likes(db, As(alice)).LikeAsync(postId);
        await using (var db = Fixture.CreateContext())
            await Likes(db, As(alice)).LikeAsync(postId);   // idempotent

        await using var check = Fixture.CreateContext();
        Assert.Equal(1, await check.Likes.CountAsync());
        Assert.Equal(1, (await check.Posts.SingleAsync()).LikeCount);
    }

    [Fact]
    public async Task Unlike_giam_count_ve_khong()
    {
        var bob = await SeedUserAsync("bob");
        var alice = await SeedUserAsync("alice");
        var postId = await SeedPostAsync(bob);

        await using (var db = Fixture.CreateContext())
            await Likes(db, As(alice)).LikeAsync(postId);
        await using (var db = Fixture.CreateContext())
        {
            var res = await Likes(db, As(alice)).UnlikeAsync(postId);
            Assert.Equal(0, res.LikeCount);
            Assert.False(res.LikedByMe);
        }

        await using var check = Fixture.CreateContext();
        Assert.Equal(0, await check.Likes.CountAsync());
        Assert.Equal(0, (await check.Posts.SingleAsync()).LikeCount);
    }
}
