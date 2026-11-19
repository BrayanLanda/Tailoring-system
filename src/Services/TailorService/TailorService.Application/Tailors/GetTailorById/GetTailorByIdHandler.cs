using TailorService.Application.Exceptions;
using TailorService.Domain.Repositories;

namespace TailorService.Application.Tailors.GetTailorById;

public sealed class GetTailorByIdHandler(ITailorRepository tailorRepository)
{
    public async Task<GetTailorByIdResult> HandleAsync(
        GetTailorByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var tailor = await tailorRepository.GetByIdAsync(query.TailorId, cancellationToken)
            ?? throw new TailorNotFoundException(query.TailorId);

        return new GetTailorByIdResult(tailor.Id, tailor.Name, tailor.IsActive);
    }
}
