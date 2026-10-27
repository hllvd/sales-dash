using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalesApp.Helpdesk.Models;

namespace SalesApp.Helpdesk.Services
{
    public class GeminiLlmService : IGeminiLlmService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiLlmService> _logger;

        public GeminiLlmService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GeminiLlmService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<GeminiSummaryResult> GenerateSummaryAsync(HelpdeskQuestion question)
        {
            var apiKey = _configuration["GEMINI_API_KEY"] 
                         ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
                         ?? _configuration["Gemini:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("GEMINI_API_KEY is not configured. Falling back to heuristic summary.");
                return GenerateFallbackSummary(question);
            }

            try
            {
                var fullConversation = new StringBuilder();
                fullConversation.AppendLine($"Pergunta: {question.Title}");
                fullConversation.AppendLine($"Contexto / Descrição: {question.CurrentContent}");
                if (!string.IsNullOrEmpty(question.SourcePageUrl))
                {
                    fullConversation.AppendLine($"Página de Origem no Sistema: {question.SourcePageUrl}");
                }
                fullConversation.AppendLine("Respostas / Discussão:");
                foreach (var msg in question.Messages.OrderBy(m => m.CreatedAt))
                {
                    fullConversation.AppendLine($"- [{msg.SenderRole}] {msg.SenderName}: {msg.Content}");
                }

                var prompt = $@"Você é um especialista técnico e documentador do sistema SalesApp.
Analise a dúvida e a discussão resolvida abaixo e crie um artigo de documentação conciso e bem estruturado.
1. Classifique a categoria como:
   - 'FAQ': se for uma dúvida conceitual, regra de negócio, comportamento do sistema ou pergunta pontual.
   - 'COMO_FAZER': se for um guia passo a passo, tutorial prático operacional ou sequência de ações para realizar uma tarefa.
2. Crie um título claro e direto em português.
3. Gere um slug amigável em minúsculas (ex: 'como-funciona-retencao-contratos' ou 'duvida-comissao-total-ativo').
4. Gere o conteúdo em Markdown claro e direto, estruturado com títulos ou tópicos, sintetizando o problema e a solução oficial definitiva.

Responda ESTRITAMENTE em formato JSON puro com esta estrutura:
{{
  ""category"": ""FAQ"" ou ""COMO_FAZER"",
  ""title"": ""Título do Artigo"",
  ""slug"": ""slug-do-artigo"",
  ""content"": ""Conteúdo em Markdown...""
}}

Conversa para análise:
{fullConversation}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        responseMimeType = "application/json"
                    }
                };

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent?key={apiKey}";
                var jsonPayload = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Gemini API call failed with status {StatusCode}: {Error}. Using fallback.", response.StatusCode, errorMsg);
                    return GenerateFallbackSummary(question);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var textPart = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(textPart))
                {
                    return GenerateFallbackSummary(question);
                }

                var result = JsonSerializer.Deserialize<GeminiSummaryResult>(textPart, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null || string.IsNullOrWhiteSpace(result.Title))
                {
                    return GenerateFallbackSummary(question);
                }

                result.Category = result.Category?.Trim().ToUpperInvariant() == "COMO_FAZER" ? "COMO_FAZER" : "FAQ";
                result.Slug = Slugify(string.IsNullOrWhiteSpace(result.Slug) ? result.Title : result.Slug);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while generating summary with Gemini. Using fallback.");
                return GenerateFallbackSummary(question);
            }
        }

        private static GeminiSummaryResult GenerateFallbackSummary(HelpdeskQuestion question)
        {
            var isHowTo = question.Title.Contains("como", StringComparison.OrdinalIgnoreCase) ||
                          question.Title.Contains("passo", StringComparison.OrdinalIgnoreCase) ||
                          question.Title.Contains("tutorial", StringComparison.OrdinalIgnoreCase);

            var category = isHowTo ? "COMO_FAZER" : "FAQ";
            var lastReply = question.Messages.LastOrDefault(m => m.SenderRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase))?.Content
                            ?? question.Messages.LastOrDefault()?.Content
                            ?? "Solução documentada no histórico de atendimento.";

            var md = new StringBuilder();
            md.AppendLine($"# {question.Title}");
            md.AppendLine();
            md.AppendLine("### 📌 Resumo da Dúvida");
            md.AppendLine(question.CurrentContent);
            md.AppendLine();
            md.AppendLine("### 💡 Solução Oficial");
            md.AppendLine(lastReply);
            if (!string.IsNullOrEmpty(question.SourcePageUrl))
            {
                md.AppendLine();
                md.AppendLine($"*Origem de referência: `{question.SourcePageUrl}`*");
            }

            return new GeminiSummaryResult
            {
                Category = category,
                Title = question.Title,
                Slug = Slugify(question.Title),
                Content = md.ToString()
            };
        }

        private static string Slugify(string text)
        {
            var clean = Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
            clean = Regex.Replace(clean, @"\s+", "-").Trim('-');
            if (string.IsNullOrEmpty(clean))
            {
                clean = "artigo-" + Guid.NewGuid().ToString("N")[..8];
            }
            return clean;
        }
    }
}
