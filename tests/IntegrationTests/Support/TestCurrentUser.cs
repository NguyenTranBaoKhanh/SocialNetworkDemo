using Application.Common;

namespace IntegrationTests.Support;

/// <summary>ICurrentUser giả lập — set Id để đóng vai user đang đăng nhập trong test.</summary>
public class TestCurrentUser : ICurrentUser
{
    public long? Id { get; set; }
    public bool IsAuthenticated => Id is not null;
}
