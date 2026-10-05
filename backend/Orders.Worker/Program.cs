using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Data;
using Orders.Worker;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// ConfiguraÁ„o do OpenTelemetry para o Aspire Dashboard
builder.Logging.AddOpenTelemetry(logging => {
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => {
        metrics.AddRuntimeInstrumentation();
    })
    .WithTracing(tracing => {
    })
    

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Database=ordersdb;Username=user;Password=password";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Registra a nova classe de neg√≥cio
builder.Services.AddScoped<OrderProcessor>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();


