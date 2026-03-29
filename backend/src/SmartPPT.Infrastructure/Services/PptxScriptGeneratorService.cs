using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using SmartPPT.Infrastructure.Models;

namespace SmartPPT.Infrastructure.Services;

public interface IPptxScriptGeneratorService
{
    Task<string> GenerateScriptAsync(SlideExtractionResult extraction);
}

public class PptxScriptGeneratorService : IPptxScriptGeneratorService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpFactory;

    public PptxScriptGeneratorService(IHttpClientFactory httpFactory, IConfiguration configuration)
    {
        _httpFactory = httpFactory;
        _configuration = configuration;
    }

    public async Task<string> GenerateScriptAsync(SlideExtractionResult extraction)
    {
        var extractionJson = JsonSerializer.Serialize(
            extraction,
            new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

        var requestBody = new
        {
            model = "nvidia/nemotron-3-super-120b-a12b:free",
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = BuildUserPrompt(extraction, extractionJson) }
            }
        };

        var client = _httpFactory.CreateClient("OpenRouter");
        var apiKey = _configuration["AI:OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException("Missing AI:OpenRouter:ApiKey configuration.");

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        client.DefaultRequestHeaders.Add("HTTP-Referer", "https://smartppt.local");

        var response = await client.PostAsJsonAsync(
            "https://openrouter.ai/api/v1/chat/completions",
            requestBody);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(responseBody);

        var rawScript = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return CleanScript(rawScript);
    }

    private static string GetSystemPrompt() => """
        You are an expert JavaScript developer specialising in PptxGenJS.
        When given slide extraction data, you produce a single, complete,
        immediately runnable Node.js CommonJS script that:

        1. Starts with: const pptxgen = require('pptxgenjs');
        2. Creates a presentation with the exact dimensions from the data
        3. Recreates every slide with every shape, text, image, and color
        4. Uses only hex colors WITHOUT a leading '#'
        5. Uses all positions and sizes exactly as provided in inches
        6. Ends with: pptx.writeFile({ fileName: 'output.pptx' });
        7. Returns ONLY JavaScript code with no explanation
        8. Uses breakLine: true for text items that indicate line breaks
        9. Uses the base64Data field directly for image data
        """;

    private static string BuildUserPrompt(SlideExtractionResult extraction, string extractionJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Generate a complete PptxGenJS script for this presentation.");
        builder.AppendLine();
        builder.AppendLine($"Template: {extraction.TemplateName}");
        builder.AppendLine($"Slide size: {extraction.SlideSize.WidthInches}\" x {extraction.SlideSize.HeightInches}\"");
        builder.AppendLine($"Slide count: {extraction.Slides.Count}");
        builder.AppendLine();
        builder.AppendLine("Shape summary:");

        foreach (var slide in extraction.Slides)
        {
            builder.AppendLine(
                $"Slide {slide.SlideIndex + 1}: {slide.Shapes.Count} shapes, {slide.Images.Count} images, background: {slide.BackgroundColor ?? "white"}");
        }

        builder.AppendLine();
        builder.AppendLine("Full JSON:");
        builder.AppendLine(extractionJson);

        return builder.ToString();
    }

    private static string CleanScript(string raw)
    {
        var script = raw.Trim();

        if (script.StartsWith("```javascript", StringComparison.OrdinalIgnoreCase) ||
            script.StartsWith("```js", StringComparison.OrdinalIgnoreCase))
        {
            var firstNewLine = script.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                script = script[(firstNewLine + 1)..];
            }
        }
        else if (script.StartsWith("```", StringComparison.Ordinal))
        {
            script = script[3..];
        }

        if (script.EndsWith("```", StringComparison.Ordinal))
        {
            script = script[..^3];
        }

        return script.Trim();
    }
}
