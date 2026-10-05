using System.Text;
using System.Text.Json;
using Orders.Core.Enums;
using Orders.Infrastructure.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Worker> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public Worker(IServiceProvider serviceProvider, ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        InitRabbitMQ();
    }

    private void InitRabbitMQ()
    {
        var factory = new ConnectionFactory 
        { 
            HostName = "localhost", UserName = "user", Password = "password",
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: "orders_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciado e ouvindo a fila orders_queue...");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            try
            {
                var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
                var doc = JsonDocument.Parse(payload);
                var orderId = Guid.Parse(doc.RootElement.GetProperty("OrderId").GetString()!);
                
                await ProcessMessageAsync(orderId, stoppingToken);

                _channel!.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem. Enviando NACK.");
                _channel!.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: "orders_queue", autoAck: false, consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(Guid orderId, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();

        // Separação de conceitos: Apenas coordena o fluxo, a lógica fica no Processor
        var startedProcessing = await processor.ProcessAsync(orderId, stoppingToken);
        
        if (startedProcessing)
        {
            await Task.Delay(5000, stoppingToken); // Requisito de delay simulado
            await processor.FinalizeAsync(orderId, stoppingToken);
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}

