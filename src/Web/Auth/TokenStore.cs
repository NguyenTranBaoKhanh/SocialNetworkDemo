using Blazored.LocalStorage;

namespace Web.Auth;

/// <summary>Lưu access + refresh token trong localStorage của trình duyệt.</summary>
public class TokenStore
{
    private const string AccessKey = "access_token";
    private const string RefreshKey = "refresh_token";

    private readonly ILocalStorageService _storage;

    public TokenStore(ILocalStorageService storage) => _storage = storage;

    public ValueTask<string?> GetAccessTokenAsync() => _storage.GetItemAsync<string>(AccessKey);
    public ValueTask<string?> GetRefreshTokenAsync() => _storage.GetItemAsync<string>(RefreshKey);

    public async Task SaveAsync(string accessToken, string refreshToken)
    {
        await _storage.SetItemAsync(AccessKey, accessToken);
        await _storage.SetItemAsync(RefreshKey, refreshToken);
    }

    public async Task ClearAsync()
    {
        await _storage.RemoveItemAsync(AccessKey);
        await _storage.RemoveItemAsync(RefreshKey);
    }
}
