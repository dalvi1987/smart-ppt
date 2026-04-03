using System.Text;
using System.Text.Json;
using SmartPPT.Infrastructure.Models;

namespace SmartPPT.Infrastructure.Services;

public interface ISemanticModelNormalizer
{
    Task NormalizeAsync(string rawScaffoldPath, string cleanScaffoldPath, CancellationToken ct = default);
}

public class SemanticModelNormalizer : ISemanticModelNormalizer
{
    private const string TruncatedSuffix = "text has been truncated due to evaluation version limitation";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Dictionary<string, string> KeyMappings = new(StringComparer.Ordinal)
    {
        ["tit"] = "title",
        ["sub"] = "subtitle",
        ["dat"] = "date",
        ["hea"] = "heading",
        ["bul"] = "bullets",
        ["lef"] = "left",
        ["rig"] = "right",
        ["cha"] = "chart",
        ["tab"] = "table",
        ["foo"] = "footer",
        ["met"] = "metric"
    };

    public async Task NormalizeAsync(string rawScaffoldPath, string cleanScaffoldPath, CancellationToken ct = default)
    {
        if (!File.Exists(rawScaffoldPath))
        {
            throw new FileNotFoundException($"Raw scaffold JSON not found at {rawScaffoldPath}", rawScaffoldPath);
        }

        var rawJson = await File.ReadAllTextAsync(rawScaffoldPath, ct);
        var scaffold = JsonSerializer.Deserialize<TemplateScaffold>(rawJson, JsonOptions)
            ?? throw new InvalidOperationException("Raw scaffold JSON could not be deserialized.");

        var normalized = Normalize(scaffold);
        var cleanJson = JsonSerializer.Serialize(normalized, JsonOptions);

        var directory = Path.GetDirectoryName(cleanScaffoldPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(cleanScaffoldPath, cleanJson, ct);
    }

    private static TemplateScaffold Normalize(TemplateScaffold scaffold)
    {
        var normalized = new TemplateScaffold
        {
            TemplateName = scaffold.TemplateName
        };

        foreach (var slide in scaffold.Slides)
        {
            normalized.Slides.Add(NormalizeSlide(slide));
        }

        return normalized;
    }

    private static TemplateScaffoldSlide NormalizeSlide(TemplateScaffoldSlide slide)
    {
        var placeholders = new List<TemplateScaffoldPlaceholder>();
        var duplicateCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var placeholder in slide.Placeholders)
        {
            var normalizedPlaceholder = NormalizePlaceholder(placeholder);
            var baseKey = normalizedPlaceholder.Key;

            if (!duplicateCounts.TryAdd(baseKey, 1))
            {
                duplicateCounts[baseKey]++;
            }
        }

        var duplicateIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var placeholder in slide.Placeholders)
        {
            var normalizedPlaceholder = NormalizePlaceholder(placeholder);
            var baseKey = normalizedPlaceholder.Key;

            if (duplicateCounts[baseKey] > 1)
            {
                duplicateIndexes.TryGetValue(baseKey, out var currentIndex);
                currentIndex++;
                duplicateIndexes[baseKey] = currentIndex;
                normalizedPlaceholder.Key = $"{baseKey}_{currentIndex}";
            }

            placeholders.Add(normalizedPlaceholder);
        }

        return new TemplateScaffoldSlide
        {
            SlideNumber = slide.SlideNumber,
            SlideType = slide.SlideType,
            Placeholders = placeholders,
            Charts = slide.Charts,
            Tables = slide.Tables
        };
    }

    private static TemplateScaffoldPlaceholder NormalizePlaceholder(TemplateScaffoldPlaceholder placeholder)
    {
        var cleanedRawText = CleanRawText(placeholder.RawText);
        var extractedKey = ExtractKey(cleanedRawText);

        if (string.IsNullOrWhiteSpace(extractedKey))
        {
            extractedKey = ExtractKey(placeholder.Key);
        }

        if (string.IsNullOrWhiteSpace(extractedKey))
        {
            extractedKey = "text";
        }

        var mappedKey = MapKey(extractedKey);
        var rawText = string.IsNullOrWhiteSpace(cleanedRawText)
            ? placeholder.RawText.Trim()
            : cleanedRawText;

        return new TemplateScaffoldPlaceholder
        {
            Key = mappedKey,
            RawText = rawText,
            Type = string.IsNullOrWhiteSpace(placeholder.Type) ? "Text" : placeholder.Type
        };
    }

    private static string CleanRawText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        return rawText.Replace(TruncatedSuffix, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static string ExtractKey(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var candidate = rawText.Trim();
        if (candidate.StartsWith("{{", StringComparison.Ordinal))
        {
            candidate = candidate[2..];
        }

        var stopIndex = candidate.Length;
        var spaceIndex = candidate.IndexOf(' ');
        if (spaceIndex >= 0 && spaceIndex < stopIndex)
        {
            stopIndex = spaceIndex;
        }

        var ellipsisIndex = candidate.IndexOf("...", StringComparison.Ordinal);
        if (ellipsisIndex >= 0 && ellipsisIndex < stopIndex)
        {
            stopIndex = ellipsisIndex;
        }

        candidate = candidate[..stopIndex];

        var builder = new StringBuilder();
        foreach (var ch in candidate)
        {
            if (char.IsLetter(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static string MapKey(string key) =>
        KeyMappings.TryGetValue(key, out var mappedKey) ? mappedKey : key;
}
