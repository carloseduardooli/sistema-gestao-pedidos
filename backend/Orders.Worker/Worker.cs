using System.Text;
using System.Text.Json;
using Orders.Core.Enums;
using Orders.Infrastructure.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Worker;

using System.Diagnostics;

public class Worker : BackgroundService
{
    public static readonly ActivitySource ActivitySource = new("Orders.Worker");
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
        var host = _serviceProvider.GetRequiredService<IConfiguration>()["RabbitMq:Host"] ?? "localhost";
        _logger.LogInformation("Tentando conectar ao RabbitMQ no host: {Host}", host);
        var factory = new ConnectionFactory 
        { 
            HostName = host,
            UserName = _serviceProvider.GetRequiredService<IConfiguration>()["RabbitMq:UserName"] ?? "guest",
            Password = _serviceProvider.GetRequiredService<IConfiguration>()["RabbitMq:Password"] ?? "guest",
            DispatchConsumersAsync = true
        };

        int retries = 5;
        while (retries > 0)
        {
            try
            {
                _connection = factory.CreateConnection();
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ indisponÌvel, tentando novamente em 3s... Tentativas restantes: {Retries}", retries);
                Thread.Sleep(3000);
                retries--;
            }
        }
        if (_connection == null) throw new Exception("Falha fatal: N„o foi possÌvel conectar ao RabbitMQ apÛs v·rias tentativas.");
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
        using var activity = ActivitySource.StartActivity("ProcessWorkerMessage");
        activity?.SetTag("order.id", orderId.ToString());
        using var scope = _serviceProvider.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<OrderProcessor>();

        // Separa√ß√£o de conceitos: Apenas coordena o fluxo, a l√≥gica fica no Processor
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




