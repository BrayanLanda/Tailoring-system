using IdentityService.Api;
using IdentityService.Api.TailorInvitations;
using IdentityService.Application.Abstractions;
using IdentityService.Application.TailorInvitations.AcceptTailorInvitation;
using IdentityService.Application.TailorInvitations.IssueTailorInvitation;
using IdentityService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? "Host=localhost;Port=5435;Database=identity_db;Username=postgres;Password=postgres";

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddIdentityInfrastructure(connectionString);
builder.Services.AddSingleton(TimeProvider.System);

// Default invitation lifetime: 48 hours
builder.Services.AddScoped(sp =>
{
    var repo = sp.GetRequiredService<IdentityService.Domain.Repositories.ITailorInvitationRepository>();
    var uow = sp.GetRequiredService<IUnitOfWork>();
    var tokenGen = sp.GetRequiredService<IInvitationTokenGenerator>();
    var delivery = sp.GetService<IInvitationDelivery>() ?? new LoggingInvitationDelivery();
    var timeProvider = sp.GetRequiredService<TimeProvider>();
    return new IssueTailorInvitationHandler(repo, uow, tokenGen, delivery, timeProvider, TimeSpan.FromHours(48));
});

builder.Services.AddScoped(sp =>
{
    var repo = sp.GetRequiredService<IdentityService.Domain.Repositories.ITailorInvitationRepository>();
    var uow = sp.GetRequiredService<IUnitOfWork>();
    var hasher = sp.GetRequiredService<IInvitationTokenHasher>();
    var provisioner = sp.GetService<IAccountProvisioner>() ?? throw new InvalidOperationException("IAccountProvisioner is not configured.");
    var timeProvider = sp.GetRequiredService<TimeProvider>();
    return new AcceptTailorInvitationHandler(repo, uow, hasher, provisioner, timeProvider);
});

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.MapTailorInvitationEndpoints();

app.Run();

public partial class Program;

internal sealed class LoggingInvitationDelivery : IInvitationDelivery
{
    public Task DeliverAsync(
        IdentityService.Domain.ValueObjects.EmailAddress email,
        IdentityService.Domain.ValueObjects.TailorInvitationId invitationId,
        string rawToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        // Placeholder until email delivery is configured in Infrastructure
        return Task.CompletedTask;
    }
}
