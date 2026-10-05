using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Orders.Core.Entities;
using Orders.Core.Enums;
using Orders.Infrastructure.Data;
// Importa o Verifier estático (novo padrão do Verify v31+)
using static VerifyXunit.Verifier;

namespace Orders.Tests;

public class OrderIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrderIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Should_Create_Order_And_Save_To_Database_And_Match_Snapshot()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { Cliente = "Maria Silva", Produto = "Monitor 4K", Valor = 3500.50m };

        // Act - Bate na API simulando o Frontend
        var response = await client.PostAsJsonAsync("/orders", request);
        response.EnsureSuccessStatusCode();
        var orderResponse = await response.Content.ReadFromJsonAsync<Order>();

        // Assert 1 - Validação Básica
        Assert.NotNull(orderResponse);
        Assert.Equal("Maria Silva", orderResponse.Cliente);
        Assert.Equal(OrderStatus.Pendente, orderResponse.Status);

        // Assert 2 - Banco de Dados Efêmero (Verifica se gravou na tabela e no Outbox)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dbOrder = await db.Orders.FindAsync(orderResponse.Id);
        Assert.NotNull(dbOrder); // Garante que foi persistido

        var outboxEvent = await db.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(outboxEvent); // Garante que o padrão Outbox foi cumprido
        Assert.Contains(orderResponse.Id.ToString(), outboxEvent.Payload); // Valida o payload na memória

        // Assert 3 - GOLDEN TEST (+2 Pontos de Bônus)
        // O Verify tira uma foto do JSON gerado. Ignoramos o ID e Data pois mudam sempre.
        await Verify(orderResponse)
            .IgnoreMember<Order>(x => x.Id)
            .IgnoreMember<Order>(x => x.DataCriacao);
    }
}
