using IdentityService.Api;
using IdentityService.Api.TailorInvitations;
using IdentityService.Application.Abstractions;
using IdentityService.Application.TailorInvitations.AcceptTailorInvitation;
using IdentityService.Application.TailorInvitations.IssueTailorInvitation;
using IdentityService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' is not configured");

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddIdentityInfrastructure(connectionString);
builder.Services.AddSingleton(TimeProvider.System);
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IInvitationDelivery, DevelopmentInvitationDelivery>();

// Default invitation lifetime: 48 hours
builder.Services.AddScoped(sp =>
{
    var repo = sp.GetRequiredService<IdentityService.Domain.Repositories.ITailorInvitationRepository>();
    var uow = sp.GetRequiredService<IUnitOfWork>();
    var tokenGen = sp.GetRequiredService<IInvitationTokenGenerator>();
    var delivery = sp.GetRequiredService<IInvitationDelivery>();
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
// Without a real delivery, invitations would be saved and their token lost. Refuse to start instead.
if (app.Services.GetService<IInvitationDelivery>() is null)
    throw new InvalidOperationException(
        "No IInvitationDelivery is registered. Register an email delivery for this environment.");
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
