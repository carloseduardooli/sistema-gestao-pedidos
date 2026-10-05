using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Core.Interfaces;
using Orders.Infrastructure.Data;

namespace Orders.Infrastructure.Services;

public class GeminiAiService : IAiAnalyticsService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAiService(HttpClient httpClient, AppDbContext db, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _db = db;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini API Key não configurada!");
        _model = configuration["Gemini:Model"] ?? "gemini-3.8-flash"; // Fallback de segurança
    }

    public async Task<string> AskAboutOrdersAsync(string userQuestion)
    {
        // 1. Prompt 1: Text-to-SQL
        var schemaPrompt = @"Você é um assistente de banco de dados PostgreSQL.
A tabela de pedidos se chama ""Orders"".
Colunas: ""Id"" (uuid), ""Cliente"" (varchar), ""Produto"" (varchar), ""Valor"" (numeric), ""Status"" (int), ""DataCriacao"" (timestamp).
Regra de Status: 1 = Pendente, 2 = Processando, 3 = Finalizado.
Responda APENAS com a query SQL para a seguinte pergunta, sem blocos de markdown e sem explicações: " + userQuestion;

        var sqlQuery = await CallGeminiAsync(schemaPrompt);
        sqlQuery = sqlQuery.Replace("```sql", "").Replace("```", "").Trim(); // Limpeza de markdown

        string dbResultStr;
        try
        {
            // 2. Executa a query no banco de forma dinâmica
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

        // 3. Prompt 2: Humanização da Resposta
        var humanizePrompt = $@"O usuário do sistema (Administrador) perguntou: '{userQuestion}'.
O banco de dados retornou o seguinte dado bruto: '{dbResultStr}'.
Formule uma resposta amigável e direta em português.
Regras IMPORTANTES:
1. Você está falando com o Administrador do sistema. Nunca trate o usuário como se fosse o cliente do banco de dados (ex: Não diga 'Olá, Julio' se o Julio for um cliente da tabela).
2. NUNCA use formatação Markdown na resposta (não use asteriscos ** para negrito, não use crases). Responda apenas com texto puro e quebras de linha naturais.";

        var finalAnswer = await CallGeminiAsync(humanizePrompt);
        return finalAnswer;
    }

    private async Task<string> CallGeminiAsync(string prompt)
    {
        // Modelo injetado dinamicamente via arquivo de configuração (appsettings.json)
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erro do Google API: {response.StatusCode} | Detalhes: {errorBody}");
        }
        
        var responseJson = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(responseJson);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return text ?? string.Empty;
    }
}










