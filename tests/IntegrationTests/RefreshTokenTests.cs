using Application.Auth;
using Application.Common;
using IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests;

public class RefreshTokenTests : IntegrationTestBase
{
    public RefreshTokenTests(DatabaseFixture fixture) : base(fixture) { }

    private async Task<AuthResponse> RegisterAsync(string username = "alice")
    {
        await using var db = Fixture.CreateContext();
        return await Auth(db).RegisterAsync(
            new RegisterRequest(username, $"{username}@test.com", "secret123", username));
    }

    [Fact]
    public async Task Register_tra_ve_ca_access_va_refresh_token()
    {
        var res = await RegisterAsync();
        Assert.False(string.IsNullOrWhiteSpace(res.Token));
        Assert.False(string.IsNullOrWhiteSpace(res.RefreshToken));

        // Refresh token được lưu (dạng hash) trong DB.
        await using var check = Fixture.CreateContext();
        Assert.Equal(1, await check.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Refresh_cap_token_moi_va_khac_token_cu()
    {
        var reg = await RegisterAsync();

        await using var db = Fixture.CreateContext();
        var refreshed = await Auth(db).RefreshAsync(new RefreshRequest(reg.RefreshToken));

        Assert.NotEqual(reg.RefreshToken, refreshed.RefreshToken);   // xoay vòng
        Assert.False(string.IsNullOrWhiteSpace(refreshed.Token));
    }

    [Fact]
    public async Task Refresh_xoay_vong_revoke_token_cu()
    {
        var reg = await RegisterAsync();

        await using (var db = Fixture.CreateContext())
            await Auth(db).RefreshAsync(new RefreshRequest(reg.RefreshToken));

        // Dùng lại refresh token CŨ (đã bị revoke khi xoay vòng) -> 401.
        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(db2).RefreshAsync(new RefreshRequest(reg.RefreshToken)));
    }

    [Fact]
    public async Task Dung_lai_token_da_revoke_thu_hoi_toan_bo_token_cua_user()
    {
        var reg = await RegisterAsync();

        AuthResponse refreshed;
        await using (var db = Fixture.CreateContext())
            refreshed = await Auth(db).RefreshAsync(new RefreshRequest(reg.RefreshToken));

        // Tấn công: dùng lại token cũ đã revoke -> hệ thống nghi bị đánh cắp.
        await using (var db = Fixture.CreateContext())
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                Auth(db).RefreshAsync(new RefreshRequest(reg.RefreshToken)));

        // Hệ quả: token mới (đang hợp lệ) cũng bị thu hồi theo.
        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(db2).RefreshAsync(new RefreshRequest(refreshed.RefreshToken)));
    }

    [Fact]
    public async Task Refresh_token_khong_ton_tai_nem_UnauthorizedException()
    {
        await using var db = Fixture.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(db).RefreshAsync(new RefreshRequest("token-bia-dat")));
    }

    [Fact]
    public async Task Logout_revoke_refresh_token()
    {
        var reg = await RegisterAsync();

        await using (var db = Fixture.CreateContext())
            await Auth(db).LogoutAsync(new LogoutRequest(reg.RefreshToken));

        // Sau logout, refresh token không dùng được nữa.
        await using var db2 = Fixture.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            Auth(db2).RefreshAsync(new RefreshRequest(reg.RefreshToken)));
    }
}
