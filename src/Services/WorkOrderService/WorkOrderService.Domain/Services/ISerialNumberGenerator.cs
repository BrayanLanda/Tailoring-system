using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Domain.Services;

public interface ISerialNumberGenerator
{
    Task<SerialNumber> GetNextAsync(CancellationToken cancellationToken = default);
}
