using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPPT.Domain.Interfaces;

namespace SmartPPT.Infrastructure.AI;

public class TemplateAwareAiService : ITemplateAwareAiService
{
    private readonly IUnitOfWork _uow;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TemplateAwareAiService> _logger;

    public TemplateAwareAiService(
        IUnitOfWork uow,
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<TemplateAwareAiService> logger)
    {
        _uow = uow;
        _httpFactory = httpFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        Guid templateId,
        string prompt,
        string provider,
        string model,
        int maxSlides,
        bool includeSpeakerNotes,
        bool strictSchema,
        List<string> allowedSlideTypes,
        CancellationToken ct = default)
    {
        var template = await _uow.Templates.GetByIdAsync(templateId, ct)
            ?? throw new KeyNotFoundException($"Template {templateId} not found");

        if (string.IsNullOrWhiteSpace(template.ScaffoldPath))
        {
            throw new InvalidOperationException("Template scaffold path is required for template-aware AI generation.");
        }

        var scaffoldJson = await ReadCleanScaffoldJsonAsync(template.ScaffoldPath, ct);
        _logger.LogInformation(
            "Generating template-aware slide JSON for template {TemplateId} via {Provider} using model {Model}",
            templateId,
            provider,
            model);

        var systemPrompt = BuildSystemPrompt(maxSlides, includeSpeakerNotes, strictSchema, allowedSlideTypes);
        var userPrompt = BuildUserPrompt(prompt, scaffoldJson);

        return provider switch
        {
            "AzureOpenAI" => await CallAzureOpenAiAsync(systemPrompt, userPrompt, model, ct),
            _ => await CallOpenRouterAsync(systemPrompt, userPrompt, model, ct)
        };
    }

    private async Task<string> ReadCleanScaffoldJsonAsync(string scaffoldPath, CancellationToken ct)
    {
        var storageBasePath = _configuration["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "smartppt");
        var cleanScaffoldPath = GetCleanScaffoldJsonFilePath(storageBasePath, scaffoldPath);

        if (!File.Exists(cleanScaffoldPath))
        {
            throw new FileNotFoundException($"Clean template scaffold JSON not found at {cleanScaffoldPath}", cleanScaffoldPath);
        }

        return await File.ReadAllTextAsync(cleanScaffoldPath, ct);
    }

    private async Task<string> CallOpenRouterAsync(string systemPrompt, string userPrompt, string model, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("OpenRouter");
        var apiKey = _configuration["AI:OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException("Missing AI:OpenRouter:ApiKey configuration.");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Headers.Add("HTTP-Referer", "https://smartppt.local");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 4000
            }),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken: ct);
        var content = responseBody?.choices?[0]?.message?.content
            ?? throw new InvalidOperationException("Empty AI response.");

        return ExtractJson(content);
    }

    private async Task<string> CallAzureOpenAiAsync(string systemPrompt, string userPrompt, string model, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("AzureOpenAI");
        var endpoint = _configuration["AI:AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Missing AI:AzureOpenAI:Endpoint configuration.");
        var apiKey = _configuration["AI:AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Missing AI:AzureOpenAI:ApiKey configuration.");
        var apiVersion = _configuration["AI:AzureOpenAI:ApiVersion"] ?? "2024-02-01";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{endpoint}/openai/deployments/{model}/chat/completions?api-version={apiVersion}");

        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 4000
            }),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken: ct);
        var content = responseBody?.choices?[0]?.message?.content
            ?? throw new InvalidOperationException("Empty AI response.");

        return ExtractJson(content);
    }

    private static string BuildSystemPrompt(
        int maxSlides,
        bool includeSpeakerNotes,
        bool strictSchema,
        IEnumerable<string> allowedSlideTypes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are an AI that generates PowerPoint content.");
        builder.AppendLine("You MUST follow the provided JSON structure exactly.");
        builder.AppendLine("Return JSON only.");
        builder.AppendLine("Do not change keys.");
        builder.AppendLine("Do not add new fields.");
        builder.AppendLine("Do not remove fields.");
        builder.AppendLine("Fill all placeholder keys with meaningful presentation-ready content.");
        builder.AppendLine("Return the response in this exact shape:");
        builder.AppendLine("{\"slides\":[{\"slideNumber\":1,\"placeholders\":{},\"charts\":[],\"tables\":[]}]}");
        builder.AppendLine("Use lowercase property names exactly as shown: slides, slideNumber, placeholders, charts, tables.");
        builder.AppendLine("Each placeholders object must contain the exact placeholder keys from the template scaffold.");
        builder.AppendLine("Preserve chart and table positions by array order.");
        builder.AppendLine($"Generate no more than {maxSlides} slides.");

        if (!includeSpeakerNotes)
        {
            builder.AppendLine("Do not include speaker notes.");
        }

        if (strictSchema)
        {
            builder.AppendLine("Return only valid JSON without markdown fences or explanation text.");
        }

        var slideTypes = allowedSlideTypes.Where(static x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (slideTypes.Count > 0)
        {
            builder.AppendLine($"Allowed slide types: {string.Join(", ", slideTypes)}.");
        }

        return builder.ToString();
    }

    private static string BuildUserPrompt(string userPrompt, string scaffoldJson)
    {
        var builder = new StringBuilder();
        builder.Append("User Request: ");
        builder.AppendLine(userPrompt);
        builder.AppendLine();
        builder.AppendLine("Template Structure:");
        builder.AppendLine(scaffoldJson);
        return builder.ToString();
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[3..];
        }

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    private static string GetCleanScaffoldJsonFilePath(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetArtifactsDirectory(storageBasePath, scaffoldPath), "clean_template.scaffold.json");

    private static string GetArtifactsDirectory(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetTemplateRoot(storageBasePath, scaffoldPath), "artifacts");

    private static string GetTemplateRoot(string storageBasePath, string scaffoldPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(storageBasePath, NormalizeRelative(scaffoldPath)));
        var baseFullPath = Path.GetFullPath(storageBasePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Resolved path '{fullPath}' is outside storage base path '{baseFullPath}'.");
        }

        return fullPath;
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
}
