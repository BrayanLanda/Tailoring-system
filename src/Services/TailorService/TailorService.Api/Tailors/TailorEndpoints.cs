using TailorService.Application.Tailors.GetTailorById;
using TailorService.Application.Tailors.RegisterTailor;

namespace TailorService.Api.Tailors;

public static class TailorEndpoints
{
    public static IEndpointRouteBuilder MapTailorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tailors").WithTags("Tailors");

        group.MapPost("/", RegisterTailorAsync)
            .WithName("RegisterTailor")
            .Produces<TailorResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id:guid}", GetTailorByIdAsync)
            .WithName("GetTailorById")
            .Produces<TailorResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> RegisterTailorAsync(
        RegisterTailorRequest request,
        RegisterTailorHandler handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["A tailor name is required."]
            });
        }

        var result = await handler.HandleAsync(new RegisterTailorCommand(request.Name), cancellationToken);
        var response = new TailorResponse(result.TailorId.Value, result.Name, result.IsActive);

        return Results.CreatedAtRoute("GetTailorById", new { id = response.Id }, response);
    }

    private static async Task<IResult> GetTailorByIdAsync(
        Guid id,
        GetTailorByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetTailorByIdQuery(new(id)), cancellationToken);
        return Results.Ok(new TailorResponse(result.TailorId.Value, result.Name, result.IsActive));
    }
}
