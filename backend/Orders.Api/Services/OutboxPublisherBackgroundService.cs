using System.Text;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Data;
using RabbitMQ.Client;

namespace Orders.Api.Services;

public class OutboxPublisherBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<OutboxPublisherBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Publisher iniciado e varrendo o banco...");
        
        var factory = new ConnectionFactory 
        { 
            HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
            Port = _configuration.GetValue<int?>("RabbitMQ:Port") ?? 5672,
            UserName = _configuration["RabbitMQ:UserName"] ?? "user", 
            Password = _configuration["RabbitMQ:Password"] ?? "password" 
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(factory, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar a tabela de Outbox.");
            }
            await Task.Delay(2000, stoppingToken); // Polling interval
        }
    }

    private async Task ProcessOutboxMessagesAsync(ConnectionFactory factory, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(stoppingToken);

        if (!messages.Any()) return;

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        
        channel.QueueDeclare(queue: "orders_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

        foreach (var message in messages)
        {
            var body = Encoding.UTF8.GetBytes(message.Payload);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.CorrelationId = message.Id.ToString();

            // Publica efetivamente
            channel.BasicPublish(exchange: "", routingKey: "orders_queue", basicProperties: properties, body: body);

            // Marca como lido e comita
            message.ProcessedAt = DateTime.UtcNow;
            _logger.LogInformation("Evento {MessageId} disparado para a fila.", message.Id);
        }

        await db.SaveChangesAsync(stoppingToken);
    }
}
