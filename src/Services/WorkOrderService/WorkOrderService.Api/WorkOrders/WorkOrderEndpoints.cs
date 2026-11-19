using WorkOrderService.Application.WorkOrders.CreateWorkOrder;
using WorkOrderService.Application.Exceptions;
using WorkOrderService.Application.WorkOrders.SetWorkOrderPrice;
using WorkOrderService.Application.WorkOrders.RegisterPayment;
using WorkOrderService.Domain.Exceptions;
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
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}/price", SetWorkOrderPriceAsync)
            .WithName("SetWorkOrderPrice")
            .Produces<SetWorkOrderPriceResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/payments", RegisterPaymentAsync)
            .WithName("RegisterPayment")
            .Produces<RegisterPaymentResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

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

    private static async Task<IResult> SetWorkOrderPriceAsync(
        Guid id,
        SetWorkOrderPriceRequest request,
        SetWorkOrderPriceHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(
                new SetWorkOrderPriceCommand(new WorkOrderId(id), request.Amount),
                cancellationToken);

            return Results.Ok(new SetWorkOrderPriceResponse(result.WorkOrderId.Value, result.Price.Amount));
        }
        catch (WorkOrderNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DomainException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["workOrder"] = [exception.Message]
            });
        }
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

        try
        {
            var result = await handler.HandleAsync(
                new RegisterPaymentCommand(new WorkOrderId(id), new PaymentId(request.PaymentId), request.Amount),
                cancellationToken);

            return Results.Ok(new RegisterPaymentResponse(
                result.WorkOrderId.Value,
                result.PaymentId.Value,
                result.PaymentStatus.ToString()));
        }
        catch (WorkOrderNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DomainException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payment"] = [exception.Message]
            });
        }
    }
}
