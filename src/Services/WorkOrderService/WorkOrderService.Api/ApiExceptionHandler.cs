using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Domain.Exceptions;

namespace WorkOrderService.Api;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (Map(exception) is not { } mapped)
            return false;

        httpContext.Response.StatusCode = mapped.Status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = mapped.Status,
                Title = mapped.Title,
                Detail = exception.Message
            },
            cancellationToken);

        return true;
    }

    private static (int Status, string Title)? Map(Exception exception) => exception switch
    {
        WorkOrderNotFoundException => (StatusCodes.Status404NotFound, "Work order not found"),
        ConcurrencyConflictException => (StatusCodes.Status409Conflict, "The work order was modified by someone else"),
        DomainException => (StatusCodes.Status422UnprocessableEntity, "A business rule was violated"),
        _ => null
    };
}
