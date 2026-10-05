using Microsoft.Extensions.Logging;
using Orders.Core.Enums;
using Orders.Infrastructure.Data;

namespace Orders.Worker;

public class OrderProcessor
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(AppDbContext db, ILogger<OrderProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(Guid orderId, CancellationToken stoppingToken)
    {
        var order = await _db.Orders.FindAsync(new object[] { orderId }, stoppingToken);
        if (order == null) return false;

        // IDEMPOTÊNCIA: Se não estiver pendente, foi processado repetido! Ignora.
        if (order.Status != OrderStatus.Pendente)
        {
            _logger.LogWarning("O Pedido {OrderId} já foi processado anteriormente. Ignorando repetição.", orderId);
            return false;
        }

        _logger.LogInformation("PROCESSANDO pedido {OrderId}...", orderId);
        order.Status = OrderStatus.Processando;
        await _db.SaveChangesAsync(stoppingToken);

        return true;
    }

    public async Task FinalizeAsync(Guid orderId, CancellationToken stoppingToken)
    {
        var order = await _db.Orders.FindAsync(new object[] { orderId }, stoppingToken);
        if (order != null && order.Status == OrderStatus.Processando)
        {
            order.Status = OrderStatus.Finalizado;
            await _db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("FINALIZADO pedido {OrderId}.", orderId);
        }
    }
}
