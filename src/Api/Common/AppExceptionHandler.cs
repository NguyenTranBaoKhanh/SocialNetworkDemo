using Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Common;

/// <summary>Map AppException (nghiệp vụ) sang ProblemDetails với đúng HTTP status.</summary>
public class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        if (ex is not AppException appEx) return false;   // để middleware mặc định lo (500)

        var problem = new ProblemDetails
        {
            Status = appEx.StatusCode,
            Title = ReasonFor(appEx.StatusCode),
            Detail = appEx.Message,
        };

        ctx.Response.StatusCode = appEx.StatusCode;
        await ctx.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    private static string ReasonFor(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Error",
    };
}
