using TailorService.Api.BackgroundServices;
using TailorService.Api;
using TailorService.Api.Tailors;
using TailorService.Application.Tailors.GetTailorById;
using TailorService.Application.Tailors.RegisterTailor;
using TailorService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TailorDb")
    ?? throw new InvalidOperationException("Connection string 'TailorDb' is not configured.");

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddTailorInfrastructure(connectionString);
builder.Services.AddTailorMessaging(builder.Configuration);
builder.Services.AddHostedService<OutboxDispatchBackgroundService>();
builder.Services.AddScoped<RegisterTailorHandler>();
builder.Services.AddScoped<GetTailorByIdHandler>();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.MapTailorEndpoints();

app.Run();

public partial class Program;
