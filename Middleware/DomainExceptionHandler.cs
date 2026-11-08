using Microsoft.AspNetCore.Diagnostics;
using Studium_Users.Domain.Exceptions;

namespace Studium_Users.Middleware;

public class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            error = domainException.ErrorCode,
            message = domainException.Message
        }, cancellationToken);

        return true;
    }
}
