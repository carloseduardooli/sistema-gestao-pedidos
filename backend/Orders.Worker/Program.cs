using Npgsql;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Data;using Orders.Worker;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Configuração do OpenTelemetry para o Aspire Dashboard
builder.Logging.AddOpenTelemetry(logging => {
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter();
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => {
        metrics.AddRuntimeInstrumentation()
               .AddOtlpExporter();
    })
    .WithTracing(tracing => {
        tracing.AddNpgsql().AddSource("Orders.Worker").AddOtlpExporter();
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Port=5433;Database=OrdersDb;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<OrderProcessor>();

var host = builder.Build();
host.Run();



