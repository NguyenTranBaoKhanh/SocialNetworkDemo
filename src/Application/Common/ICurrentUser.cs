namespace Application.Common;

/// <summary>Thông tin user đang đăng nhập, lấy từ JWT claims (hiện thực ở Api).</summary>
public interface ICurrentUser
{
    long? Id { get; }
    bool IsAuthenticated { get; }
}
