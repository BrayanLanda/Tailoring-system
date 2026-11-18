using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Infrastructure.Persistence;

internal sealed class WorkOrderIdConverter()
    : ValueConverter<WorkOrderId, Guid>(id => id.Value, value => new WorkOrderId(value));

internal sealed class TailorIdConverter()
    : ValueConverter<TailorId, Guid>(id => id.Value, value => new TailorId(value));

internal sealed class PaymentIdConverter()
    : ValueConverter<PaymentId, Guid>(id => id.Value, value => new PaymentId(value));

internal sealed class SerialNumberConverter()
    : ValueConverter<SerialNumber, long>(serial => serial.Value, value => SerialNumber.From(value));

internal sealed class PhoneNumberConverter()
    : ValueConverter<PhoneNumber, string>(phone => phone.Value, value => PhoneNumber.Of(value));

internal sealed class MoneyConverter()
    : ValueConverter<Money, decimal>(money => money.Amount, value => Money.Of(value));