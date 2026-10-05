using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Core.Interfaces;
using Orders.Infrastructure.Data;

namespace Orders.Infrastructure.Services;

public class ChatGptAiService : IAiAnalyticsService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly string _apiKey;
    private readonly string _model;

    public ChatGptAiService(HttpClient httpClient, AppDbContext db, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _db = db;
        _apiKey = configuration["OpenAi:ApiKey"] ?? throw new ArgumentNullException("OpenAI API Key não configurada!");
        _model = configuration["OpenAi:Model"] ?? "gpt-4o-mini"; // Usando o modelo mais rápido/barato da OpenAI
    }

    public async Task<string> AskAboutOrdersAsync(string userQuestion)
    {
        // 1. Prompt 1: Text-to-SQL
        var schemaPrompt = @"Você é um assistente de banco de dados PostgreSQL.
A tabela de pedidos se chama ""Orders"".
Colunas: ""Id"" (uuid), ""Cliente"" (varchar), ""Produto"" (varchar), ""Valor"" (numeric), ""Status"" (int), ""DataCriacao"" (timestamp).
Regra de Status: 1 = Pendente, 2 = Processando, 3 = Finalizado.
Responda APENAS com a query SQL para a seguinte pergunta, sem blocos de markdown e sem explicações: " + userQuestion;

        var sqlQuery = await CallOpenAiAsync(schemaPrompt);
        sqlQuery = sqlQuery.Replace("`sql", "").Replace("`", "").Trim(); // Limpeza de markdown

        string dbResultStr;
        try
        {
            using var command = _db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sqlQuery;
            await _db.Database.OpenConnectionAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            var results = new List<string>();
            while (await reader.ReadAsync())
            {
                var row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row.Add(reader.GetValue(i)?.ToString() ?? "null");
                results.Add(string.Join(", ", row));
            }
            dbResultStr = results.Any() ? string.Join(" | ", results) : "Nenhum dado encontrado.";
        }
        catch (Exception ex)
        {
            dbResultStr = $"Erro ao executar a query: {ex.Message}";
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        // 3. Prompt 2: Humanização
        var humanizePrompt = $@""O usuário do sistema (Administrador) perguntou: '{userQuestion}'.
O banco de dados retornou o seguinte dado bruto: '{dbResultStr}'.
Formule uma resposta amigável e direta em português.
Regras IMPORTANTES:
1. Você está falando com o Administrador do sistema.
2. NUNCA use formatação Markdown na resposta (não use asteriscos ** para negrito, não use crases). Responda apenas com texto puro."";

        return await CallOpenAiAsync(humanizePrompt);
    }

    private async Task<string> CallOpenAiAsync(string prompt)
    {
        var url = "https://api.openai.com/v1/chat/completions";
        
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.0
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Add("Authorization", $"Bearer {_apiKey}");
        requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(requestMessage);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erro da OpenAI API: {response.StatusCode} | Detalhes: {errorBody}");
        }
        
        var responseJson = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(responseJson);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return text ?? string.Empty;
    }
}
