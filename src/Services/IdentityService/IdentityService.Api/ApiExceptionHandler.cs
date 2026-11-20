using IdentityService.Application.Exceptions;
using IdentityService.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (Map(exception) is not { } mapped)
            return false;

        httpContext.Response.StatusCode = mapped.Status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = mapped.Status,
                Title = mapped.Title,
                Detail = mapped.Detail
            },
            cancellationToken);

        return true;
    }

    private static (int Status, string Title, string Detail)? Map(Exception exception) => exception switch
    {
        AccountCreationFailedException accountEx => (
            StatusCodes.Status400BadRequest,
            "Account creation failed",
            string.Join("; ", accountEx.Errors)),
        ConcurrencyConflictException => (
            StatusCodes.Status409Conflict,
            "The resource was modified by someone else",
            exception.Message),
        DomainException => (
            StatusCodes.Status422UnprocessableEntity,
            "A business rule was violated",
            exception.Message),
        _ => null
    };
}
