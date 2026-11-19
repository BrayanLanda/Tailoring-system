using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Domain.Exceptions;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Api.Tests;

public sealed class ApiExceptionHandlerTests
{
    private readonly ApiExceptionHandler _handler = new();
    
    [Fact]
    public async Task WorkOrderNotFound_IsAnswered404WithAReadableDetail()
    {
        var id = WorkOrderId.New();

        var (handled, context) = await HandleAsync(new WorkOrderNotFoundException(id));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.NotNull(problem);
        Assert.Equal($"Work order '{id.Value}' was not found.", problem.Detail);
    }

    [Fact]
    public async Task ConcurrencyConflict_IsAnswered409()
    {
        var (handled, context) = await HandleAsync(new ConcurrencyConflictException(new Exception("db")));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task DomainException_IsAnswered422WithTheRuleInTheDetail()
    {
        var (handled, context) = await HandleAsync(new DomainException("Payment exceeds the pending balance."));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.Status);
        Assert.Equal("Payment exceeds the pending balance.", problem.Detail);
    }

    [Fact]
    public async Task UnknownException_IsNotHandled_SoNoInternalDetailsLeak()
    {
        var (handled, _) = await HandleAsync(new InvalidOperationException("secret connection string"));

        Assert.False(handled);
    }

    private async Task<(bool Handled, DefaultHttpContext Context)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        return (handled, context);
    }

    private static async Task<ProblemDetails?> ReadProblemAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

}
