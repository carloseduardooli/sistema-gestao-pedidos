using Orders.Worker;
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
        tracing.AddOtlpExporter();
    });

builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<OrderProcessor>();

var host = builder.Build();
host.Run();
