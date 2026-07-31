namespace Application.Common;

/// <summary>Base cho lỗi nghiệp vụ, map sang HTTP status ở Api.</summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

/// <summary>400 — dữ liệu vào không hợp lệ.</summary>
public sealed class ValidationException(string message) : AppException(message)
{
    public override int StatusCode => 400;
}

/// <summary>401 — chưa đăng nhập hoặc sai thông tin đăng nhập.</summary>
public sealed class UnauthorizedException(string message) : AppException(message)
{
    public override int StatusCode => 401;
}

/// <summary>403 — không có quyền trên tài nguyên.</summary>
public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;
}

/// <summary>404 — không tìm thấy tài nguyên.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;
}

/// <summary>409 — xung đột (trùng username/email...).</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;
}
