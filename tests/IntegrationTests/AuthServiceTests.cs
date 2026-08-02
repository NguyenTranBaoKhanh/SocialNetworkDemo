using Application.Auth;
using Application.Common;
using IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

public class AuthServiceTests : IntegrationTestBase
{
    public AuthServiceTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Register_tao_user_va_tra_token()
    {
        await using var db = Fixture.CreateContext();

        var res = await Auth(db).RegisterAsync(
            new RegisterRequest("alice", "alice@test.com", "secret123", "Alice"));

        Assert.True(res.UserId > 0);
        Assert.Equal("alice", res.Username);
        Assert.False(string.IsNullOrWhiteSpace(res.Token));

        // Kiểm chứng lưu thật xuống DB + password được hash (không lưu plaintext).
        await using var check = Fixture.CreateContext();
        var user = await check.Users.SingleAsync(u => u.Username == "alice");
        Assert.NotEqual("secret123", user.PasswordHash);
    }

    [Fact]
    public async Task Register_trung_username_nem_ConflictException()
    {
        await using var db = Fixture.CreateContext();
        await Auth(db).RegisterAsync(new RegisterRequest("bob", "bob@test.com", "secret123", "Bob"));

        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<ConflictException>(() =>
            Auth(db2).RegisterAsync(new RegisterRequest("bob", "other@test.com", "secret123", "Bob2")));
    }

    [Fact]
    public async Task Register_username_hoa_thuong_van_bi_coi_la_trung()
    {
        // citext: 'Alice' và 'alice' là một -> vẫn trùng.
        await using var db = Fixture.CreateContext();
        await Auth(db).RegisterAsync(new RegisterRequest("Alice", "a@test.com", "secret123", "Alice"));

        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<ConflictException>(() =>
            Auth(db2).RegisterAsync(new RegisterRequest("alice", "b@test.com", "secret123", "Alice")));
    }

    [Theory]
    [InlineData("ab", "a@test.com", "secret123")]        // username < 3
    [InlineData("valid", "not-an-email", "secret123")]   // email sai
    [InlineData("valid", "v@test.com", "123")]           // password < 6
    public async Task Register_du_lieu_khong_hop_le_nem_ValidationException(
        string username, string email, string password)
    {
        await using var db = Fixture.CreateContext();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Auth(db).RegisterAsync(new RegisterRequest(username, email, password, "Name")));
    }

    [Fact]
    public async Task Login_dung_mat_khau_tra_token()
    {
        await using var db = Fixture.CreateContext();
        await Auth(db).RegisterAsync(new RegisterRequest("carol", "carol@test.com", "secret123", "Carol"));

        await using var db2 = Fixture.CreateContext();
        var res = await Auth(db2).LoginAsync(new LoginRequest("carol", "secret123"));
        Assert.Equal("carol", res.Username);
        Assert.False(string.IsNullOrWhiteSpace(res.Token));
    }

    [Fact]
    public async Task Login_sai_mat_khau_nem_UnauthorizedException()
    {
        await using var db = Fixture.CreateContext();
        await Auth(db).RegisterAsync(new RegisterRequest("dave", "dave@test.com", "secret123", "Dave"));

        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(db2).LoginAsync(new LoginRequest("dave", "wrong-password")));
    }

    [Fact]
    public async Task Login_user_khong_ton_tai_nem_UnauthorizedException()
    {
        await using var db = Fixture.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(db).LoginAsync(new LoginRequest("nobody", "secret123")));
    }
}
