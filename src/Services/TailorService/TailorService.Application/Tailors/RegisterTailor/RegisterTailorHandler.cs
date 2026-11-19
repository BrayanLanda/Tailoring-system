using TailorService.Application.Abstractions;
using TailorService.Domain.Entities;
using TailorService.Domain.Repositories;

namespace TailorService.Application.Tailors.RegisterTailor;

public sealed class RegisterTailorHandler(
    ITailorRepository tailorRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<RegisterTailorResult> HandleAsync(
        RegisterTailorCommand command,
        CancellationToken cancellationToken = default)
    {
        var tailor = Tailor.Create(command.Name);

        await tailorRepository.AddAsync(tailor, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterTailorResult(tailor.Id, tailor.Name, tailor.IsActive);
    }
}
