using WorkOrderService.Api.WorkOrders;
using WorkOrderService.Application.WorkOrders.CreateWorkOrder;
using WorkOrderService.Application.WorkOrders.SetWorkOrderPrice;
using WorkOrderService.Application.WorkOrders.RegisterPayment;
using WorkOrderService.Infrastructure;
using WorkOrderService.Api;
using WorkOrderService.Api.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("WorkOrderDb")
    ?? throw new InvalidOperationException("Connection string 'WorkOrderDb' is not configured.");

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddWorkOrderInfrastructure(connectionString);
builder.Services.AddWorkOrderMessaging(builder.Configuration);
builder.Services.AddHostedService<TailorEventConsumerBackgroundService>();
builder.Services.AddScoped<CreateWorkOrderHandler>();
builder.Services.AddScoped<SetWorkOrderPriceHandler>();
builder.Services.AddScoped<RegisterPaymentHandler>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.MapWorkOrderEndpoints();

app.Run();

public partial class Program;

