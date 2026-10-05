using Microsoft.Extensions.Configuration;
using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Core.Interfaces;
using Orders.Infrastructure.Data;

namespace Orders.Infrastructure.Services;

public class GroqAiService : IAiAnalyticsService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _fallbackModel;

    public GroqAiService(HttpClient httpClient, AppDbContext db, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _db = db;
        _apiKey = configuration["Groq:ApiKey"] ?? throw new ArgumentNullException("Groq API Key não configurada!");
        _model = configuration["Groq:Model"] ?? "openai/gpt-oss-120b";
        _fallbackModel = configuration["Groq:FallbackModel"] ?? "llama3-8b-8192";
    }

    public async Task<string> AskAboutOrdersAsync(string userQuestion)
    {
                var schemaPrompt = @"Você é um assistente de banco de dados PostgreSQL.
O nome EXATO da tabela é ""Orders"".
As colunas EXATAS são: ""Id"" (uuid), ""Cliente"" (varchar), ""Produto"" (varchar), ""Valor"" (numeric), ""Status"" (int), ""DataCriacao"" (timestamp).

Regras OBRIGATÓRIAS para o PostgreSQL:
1. Você DEVE usar ASPAS DUPLAS ao redor do nome da tabela e de todas as colunas. Exemplo: SELECT ""Id"", ""Cliente"" FROM ""Orders"" WHERE ""Produto"" = 'TV'.
2. Responda APENAS com a query SQL para a seguinte pergunta, sem blocos de markdown, sem `sql e sem explicações: " + userQuestion;

        var sqlQuery = await CallGroqWithRetryAsync(schemaPrompt);
        sqlQuery = sqlQuery.Replace("`sql", "").Replace("`", "").Trim();

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
            dbResultStr = "Erro ao executar a query: " + ex.Message;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

                var humanizePrompt = $@"O usuário do sistema (Administrador) perguntou: '{userQuestion}'.
O banco de dados retornou o seguinte dado bruto: '{dbResultStr}'.

Formule uma resposta amigável e direta em português.
Regras RIGOROSAS:
1. Você DEVE se basear ÚNICA E EXCLUSIVAMENTE nos dados brutos acima.
2. TRADUÇÃO DE STATUS: Na coluna Status (que é um número), lembre-se sempre que: 1 significa Pendente, 2 significa Processando e 3 significa Finalizado. NUNCA confunda o número do status com 'Quantidade de itens'.
3. NUNCA alucine ou invente informações (ex: Não invente "Forma de pagamento" ou "Quantidade de itens" pois essas colunas não existem).
4. Se o dado bruto for vazio ou contiver erros, avise educadamente que a informação não foi encontrada.
5. NUNCA use formatação Markdown na resposta (não use asteriscos ** para negrito, não use crases). Responda apenas com texto puro.";

        return await CallGroqWithRetryAsync(humanizePrompt);
    }

    private async Task<string> CallGroqWithRetryAsync(string prompt)
    {
        try
        {
            // Tenta o modelo principal primeiro
            return await CallGroqApiAsync(prompt, _model);
        }
        catch (Exception)
        {
            // Fallback (Retry) com o modelo mais barato/rápido em caso de timeout, 503 ou 429
            return await CallGroqApiAsync(prompt, _fallbackModel);
        }
    }

    private async Task<string> CallGroqApiAsync(string prompt, string targetModel)
    {
        var url = "https://api.groq.com/openai/v1/chat/completions";
        
        var requestBody = new
        {
            model = targetModel,
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
            throw new Exception($"Erro do Groq API no modelo {targetModel}: {response.StatusCode} | Detalhes: {errorBody}");
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

