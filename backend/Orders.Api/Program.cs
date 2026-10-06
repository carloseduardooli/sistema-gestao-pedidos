using Npgsql;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Services;
using Orders.Core.Entities;
using Orders.Core.Enums;
using Orders.Infrastructure.Data;
using System.Text.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configuracao do OpenTelemetry para o Aspire Dashboard
builder.Logging.AddOpenTelemetry(logging => {
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter();
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation()
               .AddOtlpExporter();
    })
    .WithTracing(tracing => {
        tracing.AddAspNetCoreInstrumentation(opts => { opts.Filter = ctx => !(ctx.Request.Method == "GET" && ctx.Request.Path == "/orders"); })
               .AddHttpClientInstrumentation()
               .AddEntityFrameworkCoreInstrumentation()
               .AddSource("Orders.OutboxPublisher")
               .AddOtlpExporter();
    });

// 1. Configuracao do Banco
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Port=5433;Database=OrdersDb;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// 2. Registro do Background Service (O Outbox Publisher)
builder.Services.AddHostedService<OutboxPublisherBackgroundService>();

builder.Services.AddCors();
builder.Services.AddHttpClient<Orders.Core.Interfaces.IAiAnalyticsService, Orders.Infrastructure.Services.GroqAiService>();

// Tratamento padronizado de excecoes (RFC 7807)
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler(); // Captura exceptions globais e retorna JSON seguro
app.UseStatusCodePages();
app.MapHealthChecks("/health");

// Migrations automaticas do banco de dados (Requisito)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

// ===================== ENDPOINTS =====================
app.MapPost("/orders", async (CreateOrderRequest req, AppDbContext db) =>
{
    // Validacao de Dados (Fail-fast)
    if (string.IsNullOrWhiteSpace(req.Cliente))
        return Results.BadRequest(new { error = "O nome do cliente eh obrigatorio." });
        
    if (string.IsNullOrWhiteSpace(req.Produto))
        return Results.BadRequest(new { error = "O produto eh obrigatorio." });
        
    if (req.Valor <= 0)
        return Results.BadRequest(new { error = "O valor do pedido deve ser maior que zero." });

    var order = new Order { Cliente = req.Cliente, Produto = req.Produto, Valor = req.Valor };
    
    var outboxMessage = new OutboxMessage
    {
        EventType = "OrderCreated",
        Payload = JsonSerializer.Serialize(new { OrderId = order.Id })
    };

    db.Orders.Add(order);
    db.OutboxMessages.Add(outboxMessage);
    await db.SaveChangesAsync(); // Transacao natural: Salva os dois juntos!

    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders", async (AppDbContext db) =>
{
    var orders = await db.Orders.AsNoTracking().OrderByDescending(o => o.DataCriacao).ToListAsync();
    return Results.Ok(orders);
});

app.MapGet("/orders/{id}", async (Guid id, AppDbContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

// Endpoint de IA / Analytics
app.MapPost("/orders/ask", async (AskRequest req, Orders.Core.Interfaces.IAiAnalyticsService ai) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "A pergunta nao pode estar vazia." });

    var resposta = await ai.AskAboutOrdersAsync(req.Question);
    return Results.Ok(new { resposta });
});

app.Run();

public record CreateOrderRequest(string Cliente, string Produto, decimal Valor);
public record AskRequest(string Question);

public partial class Program { }
