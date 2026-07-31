using System.Security.Claims;
using Application.Common;

namespace Api.Common;

/// <summary>Đọc user hiện tại từ JWT claims trong HttpContext.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public long? Id
    {
        get
        {
            var raw = _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(raw, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => Id is not null;
}
