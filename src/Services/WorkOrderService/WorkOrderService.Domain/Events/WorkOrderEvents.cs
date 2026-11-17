using WorkOrderService.Domain.Common;
using WorkOrderService.Domain.Enums;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Events;

public sealed record WorkOrderCreated(
    WorkOrderId WorkOrderId,
    SerialNumber SerialNumber,
    WorkOrderType Type,
    WorkOrderId? OriginalWorkOrderId,
    DateTimeOffset CreatedAt) : IDomainEvent;

public sealed record TailorAssigned(
    WorkOrderId WorkOrderId,
    TailorId TailorId,
    string? ReassignmentReason) : IDomainEvent;

public sealed record PriceSet(
    WorkOrderId WorkOrderId,
    Money Price) : IDomainEvent;

public sealed record PaymentReceived(
    WorkOrderId WorkOrderId,
    PaymentId PaymentId,
    Money Amount,
    PaymentStatus PaymentStatus,
    DateTimeOffset PaidAt) : IDomainEvent;

public sealed record GarmentDeliveredByTailor(
    WorkOrderId WorkOrderId,
    TailorId TailorId,
    WorkOrderType Type,
    DateTimeOffset DeliveredAt) : IDomainEvent;

public sealed record GarmentDeliveredToCustomer(
    WorkOrderId WorkOrderId,
    DateTimeOffset DeliveredAt) : IDomainEvent;

public sealed record WarrantyCompensationAuthorized(
    WorkOrderId WorkOrderId,
    TailorId TailorId,
    Money Amount,
    string AuthorizedBy,
    DateTimeOffset AuthorizedAt) : IDomainEvent;
