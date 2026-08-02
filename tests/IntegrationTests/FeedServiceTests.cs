using Application.Posts;
using IntegrationTests.Support;

namespace IntegrationTests;

public class FeedServiceTests : IntegrationTestBase
{
    public FeedServiceTests(DatabaseFixture fixture) : base(fixture) { }

    private async Task<Guid> PostAsAsync(long authorId, string content)
    {
        await using var db = Fixture.CreateContext();
        return (await Posts(db, As(authorId)).CreateAsync(new CreatePostRequest(content, null))).Id;
    }

    [Fact]
    public async Task Feed_hien_post_cua_nguoi_minh_follow()
    {
        var alice = await SeedUserAsync("alice");
        var bob = await SeedUserAsync("bob");

        await using (var db = Fixture.CreateContext())
            await Follows(db, As(alice)).FollowAsync("bob");
        await PostAsAsync(bob, "bài của bob");

        await using var db2 = Fixture.CreateContext();
        var feed = await Feed(db2, As(alice)).GetAsync(cursor: null);

        Assert.Single(feed.Items);
        Assert.Equal("bài của bob", feed.Items[0].Content);
    }

    [Fact]
    public async Task Feed_khong_hien_post_nguoi_minh_KHONG_follow()
    {
        var alice = await SeedUserAsync("alice");
        var carol = await SeedUserAsync("carol");   // alice không follow carol
        await PostAsAsync(carol, "bài của carol");

        await using var db = Fixture.CreateContext();
        var feed = await Feed(db, As(alice)).GetAsync(cursor: null);
        Assert.Empty(feed.Items);
    }

    [Fact]
    public async Task Feed_bao_gom_post_cua_chinh_minh()
    {
        var alice = await SeedUserAsync("alice");
        await PostAsAsync(alice, "bài của chính alice");

        await using var db = Fixture.CreateContext();
        var feed = await Feed(db, As(alice)).GetAsync(cursor: null);
        Assert.Single(feed.Items);
    }

    [Fact]
    public async Task Feed_sap_xep_moi_nhat_truoc()
    {
        var alice = await SeedUserAsync("alice");
        await PostAsAsync(alice, "cũ");
        await PostAsAsync(alice, "mới");

        await using var db = Fixture.CreateContext();
        var feed = await Feed(db, As(alice)).GetAsync(cursor: null);
        Assert.Equal("mới", feed.Items[0].Content);
        Assert.Equal("cũ", feed.Items[1].Content);
    }

    [Fact]
    public async Task Feed_phan_trang_bang_cursor()
    {
        var alice = await SeedUserAsync("alice");
        await PostAsAsync(alice, "p1");
        await PostAsAsync(alice, "p2");
        await PostAsAsync(alice, "p3");

        await using var db = Fixture.CreateContext();
        var page1 = await Feed(db, As(alice)).GetAsync(cursor: null, limit: 2);
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.Equal("p3", page1.Items[0].Content);   // mới nhất trước
        Assert.Equal("p2", page1.Items[1].Content);

        var page2 = await Feed(db, As(alice)).GetAsync(cursor: page1.NextCursor, limit: 2);
        Assert.Single(page2.Items);
        Assert.Equal("p1", page2.Items[0].Content);
        Assert.Null(page2.NextCursor);
    }
}
