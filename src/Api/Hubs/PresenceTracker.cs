using System.Collections.Concurrent;

namespace Api.Hubs;

/// <summary>
/// Theo dõi user online (MVP: in-memory, đếm số connection mỗi user).
/// Chỉ đúng cho 1 instance — nhiều instance cần chuyển sang Redis (theo CLAUDE.md).
/// </summary>
public class PresenceTracker
{
    private readonly ConcurrentDictionary<long, int> _connections = new();

    /// <summary>Ghi nhận 1 connection; trả về true nếu user vừa chuyển sang online.</summary>
    public bool Connect(long userId)
    {
        var became = false;
        _connections.AddOrUpdate(userId, _ => { became = true; return 1; }, (_, n) => n + 1);
        return became;
    }

    /// <summary>Bỏ 1 connection; trả về true nếu user vừa chuyển sang offline.</summary>
    public bool Disconnect(long userId)
    {
        if (!_connections.TryGetValue(userId, out var n)) return false;
        if (n <= 1)
        {
            _connections.TryRemove(userId, out _);
            return true;
        }
        _connections.TryUpdate(userId, n - 1, n);
        return false;
    }

    public IReadOnlyList<long> OnlineUsers() => _connections.Keys.ToList();
}
