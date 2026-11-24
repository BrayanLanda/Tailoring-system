using WorkOrderService.Application.WorkOrders.CreateWorkOrder;
using WorkOrderService.Application.WorkOrders.SetWorkOrderPrice;
using WorkOrderService.Application.WorkOrders.RegisterPayment;
using WorkOrderService.Application.WorkOrders.AssignTailor;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Api.WorkOrders;

public static class WorkOrderEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/work-orders").WithTags("WorkOrders");

        group.MapPost("/", CreateWorkOrderAsync)
            .WithName("CreateWorkOrder")
            .Produces<CreateWorkOrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{id:guid}/price", SetWorkOrderPriceAsync)
            .WithName("SetWorkOrderPrice")
            .Produces<SetWorkOrderPriceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/payments", RegisterPaymentAsync)
            .WithName("RegisterPayment")
            .Produces<RegisterPaymentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/assignments", AssignTailorAsync)
            .WithName("AssignTailor")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .Produces<AssignTailorResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

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

        var result = await handler.HandleAsync(command, cancellationToken);

        return Results.Created(
            $"/api/work-orders/{result.Id.Value}",
            new CreateWorkOrderResponse(result.Id.Value, result.SerialNumber.Value));
    }

    private static async Task<IResult> SetWorkOrderPriceAsync(
        Guid id,
        SetWorkOrderPriceRequest request,
        SetWorkOrderPriceHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new SetWorkOrderPriceCommand(new WorkOrderId(id), request.Amount),
            cancellationToken);

        return Results.Ok(new SetWorkOrderPriceResponse(result.WorkOrderId.Value, result.Price.Amount));
    }

    private static async Task<IResult> RegisterPaymentAsync(
        Guid id,
        RegisterPaymentRequest request,
        RegisterPaymentHandler handler,
        CancellationToken cancellationToken)
    {
        if (request.PaymentId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.PaymentId)] = ["A payment ID is required for idempotent processing."]
            });
        }

        var result = await handler.HandleAsync(
            new RegisterPaymentCommand(new WorkOrderId(id), new PaymentId(request.PaymentId), request.Amount),
            cancellationToken);

        return Results.Ok(new RegisterPaymentResponse(
            result.WorkOrderId.Value,
            result.PaymentId.Value,
            result.PaymentStatus.ToString()));
    }

    private static async Task<IResult> AssignTailorAsync(
        Guid id,
        AssignTailorRequest request,
        AssignTailorHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AssignTailorCommand(new WorkOrderId(id), request.TailorId, request.ReassignmentReason),
            cancellationToken);

        return Results.Ok(new AssignTailorResponse(result.WorkOrderId.Value, result.TailorId));
    }
}
