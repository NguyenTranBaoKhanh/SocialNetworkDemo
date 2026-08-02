using Application.Comments;
using Application.Common;
using Application.Posts;
using IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

public class CommentServiceTests : IntegrationTestBase
{
    public CommentServiceTests(DatabaseFixture fixture) : base(fixture) { }

    private async Task<Guid> SeedPostAsync(long authorId)
    {
        await using var db = Fixture.CreateContext();
        return (await Posts(db, As(authorId)).CreateAsync(new CreatePostRequest("post", null))).Id;
    }

    [Fact]
    public async Task Add_comment_tang_commentCount()
    {
        var alice = await SeedUserAsync("alice");
        var postId = await SeedPostAsync(alice);

        await using var db = Fixture.CreateContext();
        var c = await Comments(db, As(alice)).AddAsync(postId, new CreateCommentRequest("hay", null));

        Assert.True(c.Id > 0);
        Assert.Null(c.ParentId);

        await using var check = Fixture.CreateContext();
        Assert.Equal(1, (await check.Posts.SingleAsync()).CommentCount);
    }

    [Fact]
    public async Task Reply_gan_dung_parentId()
    {
        var alice = await SeedUserAsync("alice");
        var postId = await SeedPostAsync(alice);

        long parentId;
        await using (var db = Fixture.CreateContext())
            parentId = (await Comments(db, As(alice)).AddAsync(postId, new CreateCommentRequest("gốc", null))).Id;

        await using var db2 = Fixture.CreateContext();
        var reply = await Comments(db2, As(alice)).AddAsync(postId, new CreateCommentRequest("trả lời", parentId));
        Assert.Equal(parentId, reply.ParentId);
    }

    [Fact]
    public async Task Reply_parent_khong_hop_le_nem_ValidationException()
    {
        var alice = await SeedUserAsync("alice");
        var postId = await SeedPostAsync(alice);

        await using var db = Fixture.CreateContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Comments(db, As(alice)).AddAsync(postId, new CreateCommentRequest("x", 99999)));
    }

    [Fact]
    public async Task List_phan_trang_bang_cursor()
    {
        var alice = await SeedUserAsync("alice");
        var postId = await SeedPostAsync(alice);

        // Tạo 3 comment.
        for (int i = 0; i < 3; i++)
            await using (var db = Fixture.CreateContext())
                await Comments(db, As(alice)).AddAsync(postId, new CreateCommentRequest($"c{i}", null));

        await using var db2 = Fixture.CreateContext();
        var page1 = await Comments(db2, As(alice)).ListAsync(postId, afterId: null, limit: 2);
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);

        var page2 = await Comments(db2, As(alice)).ListAsync(postId, afterId: long.Parse(page1.NextCursor!), limit: 2);
        Assert.Single(page2.Items);
        Assert.Null(page2.NextCursor);
    }
}
