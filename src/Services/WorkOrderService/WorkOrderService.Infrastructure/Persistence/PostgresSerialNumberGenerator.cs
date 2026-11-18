using Microsoft.EntityFrameworkCore;
using WorkOrderService.Domain.Services;
using WorkOrderService.Domain.ValueObjects;

namespace WorkOrderService.Infrastructure.Persistence;

public sealed class PostgresSerialNumberGenerator(WorkOrderDbContext dbContext) : ISerialNumberGenerator
{
    public async Task<SerialNumber> GetNextAsync(CancellationToken cancellationToken = default)
    {
        var value = await dbContext.Database
            .SqlQuery<long>($"SELECT nextval('work_order_serial_number') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return SerialNumber.From(value);
    }
}
