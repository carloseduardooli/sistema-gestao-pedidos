using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Core.Interfaces;
using static VerifyXunit.Verifier;

namespace Orders.Tests;

public class ChatIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ChatIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Should_Return_AI_Insights_And_Match_Snapshot()
    {
        // Arrange
        // Criamos um client customizado substituindo o Groq por um Fake para evitar custos, indisponibilidades da API e alucinações no Golden Test.
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove o registro de HttpClient real que aponte para IAiAnalyticsService
                services.RemoveAll(typeof(IAiAnalyticsService));
                
                // Injeta o Mock manual
                services.AddSingleton<IAiAnalyticsService, FakeAiService>();
            });
        }).CreateClient();

        var request = new { Question = "Quantos pedidos temos?" };

        // Act
        var response = await client.PostAsJsonAsync("/orders/ask", request);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var jsonResponse = await response.Content.ReadAsStringAsync();

        // GOLDEN TEST (Garante que a estrutura JSON { "resposta": "..." } não mudará sem querer)
        await Verify(jsonResponse);
    }
}

// Fake Class para isolar os testes de integração do banco de dados e da rede real da API da LLM
public class FakeAiService : IAiAnalyticsService
{
    public Task<string> AskAboutOrdersAsync(string userQuestion)
    {
        return Task.FromResult("Mock da IA: Temos 1 pedido no valor de R$ 3500,00 registrado no banco.");
    }
}
