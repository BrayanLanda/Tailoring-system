using WorkOrderService.Application.WorkOrders.CreateWorkOrder;
using WorkOrderService.Domain.Exceptions;

namespace WorkOrderService.Api.WorkOrders;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/work-orders").WithTags("WorkOrders");

        group.MapPost("/", CreateWorkOrderAsync)
            .WithName("CreateWorkOrder")
            .Produces<CreateWorkOrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return endpoints;
    }

    private static async Task<IResult> CreateWorkOrderAsync(
        CreateWorkOrderRequest request,
        CreateWorkOrderHandler handler,
        CancellationToken cancellationToken)
    {
        if (request.Jobs is null || request.Jobs.Count == 0 || request.Jobs.Any(job => job is null))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Jobs)] = ["At least one valid job is required."]
            });
        }

        var command = new CreateWorkOrderCommand(
            request.CustomerName,
            request.CustomerPhone,
            request.GarmentDescription,
            request.Jobs
                .Select(job => new CreateWorkOrderJob(job!.Description, job.Quantity))
                .ToArray());

        try
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return Results.Created(
                $"/api/work-orders/{result.Id.Value}",
                new CreateWorkOrderResponse(result.Id.Value, result.SerialNumber.Value));
        }
        catch (DomainException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["workOrder"] = [exception.Message]
            });
        }
    }
}
