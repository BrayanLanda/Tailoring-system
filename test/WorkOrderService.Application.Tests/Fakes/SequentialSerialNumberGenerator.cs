using WorkOrderService.Domain.Services;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Application.Tests.Fakes;

internal sealed class SequentialSerialNumberGenerator : ISerialNumberGenerator
{
    private long _next = 1;
    public int Calls { get; private set; }

    public Task<SerialNumber> GetNextAsync(CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(SerialNumber.From(_next++));
    }
}
