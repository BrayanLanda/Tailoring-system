using IdentityService.Application.Auth.Login;

namespace IdentityService.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors[nameof(request.Email)] = ["Email is required."];

        if (string.IsNullOrWhiteSpace(request.Password))
            errors[nameof(request.Password)] = ["Password is required."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var result = await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return Results.Ok(new LoginResponse(result.Token, result.ExpiresAt));
    }
}
