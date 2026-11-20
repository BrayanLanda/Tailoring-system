using System.Text.Json;
using IdentityService.Api;
using IdentityService.Application.Exceptions;
using IdentityService.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Tests;

public sealed class ApiExceptionHandlerTests
{
    private readonly ApiExceptionHandler _handler = new();

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
        var (handled, context) = await HandleAsync(new DomainException("The invitation has expired."));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.NotNull(problem);
        Assert.Equal("The invitation has expired.", problem.Detail);
    }

    [Fact]
    public async Task AccountCreationFailed_IsAnswered400WithErrorsDetail()
    {
        var errors = new[] { "Password requires non alphanumeric character.", "Password too short." };
        var (handled, context) = await HandleAsync(new AccountCreationFailedException(errors));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.NotNull(problem);
        Assert.Contains("Password requires non alphanumeric character.", problem.Detail);
        Assert.Contains("Password too short.", problem.Detail);
    }

    [Fact]
    public async Task UnknownException_IsNotHandled()
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
