using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core.Entities;
using Orders.Core.Enums;
using Orders.Infrastructure.Data;
using Orders.Worker;

namespace Orders.Tests;

public class OrderProcessorTests
{
    [Fact]
    public async Task ProcessAsync_DeveAtualizarParaProcessando_QuandoStatusForPendente()
    {
        // Arrange - Configura banco em memória super rápido (Unit Test)
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var db = new AppDbContext(options);
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Pendente };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var processor = new OrderProcessor(db, NullLogger<OrderProcessor>.Instance);

        // Act
        var result = await processor.ProcessAsync(order.Id, CancellationToken.None);

        // Assert
        Assert.True(result); // O processamento deve iniciar
        var updatedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Processando, updatedOrder!.Status);
    }

    [Fact]
    public async Task ProcessAsync_DeveIgnorarEIdempotente_QuandoStatusDiferenteDePendente()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var db = new AppDbContext(options);
        // Simulando que o pedido já foi processado antes
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.Processando }; 
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var processor = new OrderProcessor(db, NullLogger<OrderProcessor>.Instance);

        // Act - Simula o RabbitMQ mandando a mesma mensagem de novo
        var result = await processor.ProcessAsync(order.Id, CancellationToken.None);

        // Assert - A mágica da Idempotência!
        Assert.False(result); // Não deve iniciar novamente!
        var updatedOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Processando, updatedOrder!.Status); // O Status foi mantido e o banco protegido
    }
}
