using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPPT.Domain.Interfaces;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace SmartPPT.Infrastructure.AI;

public class AiOrchestratorService : IAiOrchestratorService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AiOrchestratorService> _logger;

    private static readonly string SlideJsonSchema = """
    Return ONLY valid JSON matching this schema, no markdown, no explanation:
    {
      "slides": [
        {
          "type": "TitleSlide|BulletSlide|ChartSlide|TableSlide|TwoColumnSlide|SectionBreak",
          "data": {
            "title": "string",
            "subtitle": "string (TitleSlide only)",
            "bullets": ["string array (BulletSlide only)"],
            "speakerNotes": "string (optional)",
            "chart": {
              "type": "bar|line|pie|doughnut",
              "categories": ["string"],
              "series": [{"name":"string","values":[number]}]
            },
            "table": {
              "headers": ["string"],
              "rows": [["string"]]
            }
          }
        }
      ]
    }
    """;

    public AiOrchestratorService(IHttpClientFactory httpFactory, IConfiguration config,
        ILogger<AiOrchestratorService> logger)
    {
        _httpFactory = httpFactory; _config = config; _logger = logger;
    }

    public async Task<string> GenerateSlideJsonAsync(string prompt, AiGenerationOptions options)
    {
        _logger.LogInformation("Generating slides via {Provider} with model {Model}", options.Provider, options.Model);

        var systemPrompt = BuildSystemPrompt(options);
        var userPrompt = $"{prompt}\n\nGenerate exactly up to {options.MaxSlides} slides.";

        return options.Provider switch
        {
            "AzureOpenAI" => await CallAzureOpenAIAsync(systemPrompt, userPrompt, options),
            _ => await CallOpenRouterAsync(systemPrompt, userPrompt, options)
        };
    }

    private async Task<string> CallOpenRouterAsync(string system, string user, AiGenerationOptions options)
    {
        var client = _httpFactory.CreateClient("OpenRouter");
        var apiKey = _config["AI:OpenRouter:ApiKey"];
        
        var body = new
        {
            model = options.Model,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.4,
            max_tokens = 4000
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Headers.Add("HTTP-Referer", "https://smartppt.app");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>();
        var content = result?.choices?[0]?.message?.content ?? throw new InvalidOperationException("Empty AI response");
        return ExtractJson(content);
    }

    private async Task<string> CallAzureOpenAIAsync(string system, string user, AiGenerationOptions options)
    {
        var client = _httpFactory.CreateClient("AzureOpenAI");
        var endpoint = _config["AI:AzureOpenAI:Endpoint"];
        var apiKey = _config["AI:AzureOpenAI:ApiKey"];
        var deployment = options.Model;
        var apiVersion = _config["AI:AzureOpenAI:ApiVersion"] ?? "2024-02-01";

        var url = $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

        var body = new
        {
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.4,
            max_tokens = 4000
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiResponse>();
        var content = result?.choices?[0]?.message?.content ?? throw new InvalidOperationException("Empty AI response");
        return ExtractJson(content);
    }

    private string BuildSystemPrompt(AiGenerationOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are SmartPPT, an AI that generates structured presentation data in JSON format.");
        sb.AppendLine(SlideJsonSchema);
        if (options.StrictSchema) sb.AppendLine("CRITICAL: Return ONLY the JSON object. No markdown code blocks, no explanation text.");
        if (!options.IncludeSpeakerNotes) sb.AppendLine("Do NOT include speakerNotes fields.");
        if (options.AllowedSlideTypes.Any())
            sb.AppendLine($"Only use these slide types: {string.Join(", ", options.AllowedSlideTypes)}");
        return sb.ToString();
    }

    private static string ExtractJson(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```json")) content = content[7..];
        if (content.StartsWith("```")) content = content[3..];
        if (content.EndsWith("```")) content = content[..^3];
        return content.Trim();
    }
}

// Response models
public record OpenAiResponse(Choice[]? choices);
public record Choice(Message message);
public record Message(string role, string content);
