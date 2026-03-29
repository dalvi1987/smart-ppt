using Microsoft.Extensions.Configuration;
using SmartPPT.Domain.Entities;
using SmartPPT.Domain.Enums;
using SmartPPT.Domain.Interfaces;

namespace SmartPPT.Infrastructure.Services;

// ─── Local Disk Storage (swap for Azure Blob in prod) ───────────────────────
public class LocalStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public LocalStorageService(IConfiguration config)
    {
        _basePath = config["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "smartppt");
        _baseUrl = config["Storage:BaseUrl"] ?? "http://localhost:5000/files";
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadTemplateAsync(Stream stream, string fileName)
    {
        var relativePath = Path.Combine(DateTime.UtcNow.ToString("yyyy/MM"), fileName);
        var fullPath = Path.Combine(_basePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fs = File.Create(fullPath))
        {
            await stream.CopyToAsync(fs);
            await fs.FlushAsync();
        } // fs fully closed and flushed before we return

        return relativePath;
    }

    public async Task<Stream> DownloadAsync(string path)
    {
        var fullPath = Path.Combine(_basePath, path);
        return File.OpenRead(fullPath);
    }

    public async Task DeleteAsync(string path)
    {
        var fullPath = Path.Combine(_basePath, path);
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    public string GetPublicUrl(string path) => $"{_baseUrl}/{path.Replace('\\', '/').TrimStart('/')}";
}

// ─── Template Parser ─────────────────────────────────────────────────────────
public class TemplateParserService : ITemplateParserService
{
    // Maps common placeholder name patterns to PlaceholderType
    private static readonly Dictionary<string, PlaceholderType> TypeHeuristics = new()
    {
        { "chart", PlaceholderType.Chart }, { "graph", PlaceholderType.Chart },
        { "table", PlaceholderType.Table }, { "grid", PlaceholderType.Table },
        { "image", PlaceholderType.Image }, { "logo", PlaceholderType.Image }, { "photo", PlaceholderType.Image },
        { "date", PlaceholderType.Date }, { "number", PlaceholderType.Number }, { "count", PlaceholderType.Number }
    };

    public async Task<(List<SlideLayout> layouts, int placeholderCount)> ParseAsync(Stream pptxStream, Guid templateId)
    {
        var layouts = new List<SlideLayout>();
        int totalPlaceholders = 0;

        try
        {
            using var pptx = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(pptxStream, false);
            var presentation = pptx.PresentationPart?.Presentation;
            if (presentation == null) return (layouts, 0);

            var slideIdList = presentation.SlideIdList;
            if (slideIdList == null) return (layouts, 0);

            int order = 0;
            foreach (var slideId in slideIdList.Elements<DocumentFormat.OpenXml.Presentation.SlideId>())
            {
                var relId = slideId.RelationshipId?.Value;
                if (relId == null) continue;

                var slidePart = (DocumentFormat.OpenXml.Packaging.SlidePart)pptx.PresentationPart!.GetPartById(relId);
                var layoutName = slidePart.SlideLayoutPart?.SlideLayout?.CommonSlideData?.Name?.Value ?? $"Slide {order + 1}";
                var slideType = DetectSlideType(layoutName);

                var layout = SlideLayout.Create(templateId, layoutName, slideType, order++);

                // Find text shapes with {{token}} patterns
                var xmlText = slidePart.Slide?.OuterXml ?? "";
                var tokens = ExtractTokens(xmlText);

                foreach (var token in tokens)
                {
                    var name = token.Trim('{', '}');
                    var type = DetectPlaceholderType(name);
                    var placeholder = Placeholder.Create(layout.Id, name, token, type);
                    layout.AddPlaceholder(placeholder);
                    totalPlaceholders++;
                }

                layouts.Add(layout);
            }
        }
        catch
        {
            // If parsing fails, return a default structure
            var defaultLayout = SlideLayout.Create(templateId, "Title Slide", SlideType.Title, 0);
            defaultLayout.AddPlaceholder(Placeholder.Create(defaultLayout.Id, "title", "{{title}}", PlaceholderType.Text));
            defaultLayout.AddPlaceholder(Placeholder.Create(defaultLayout.Id, "subtitle", "{{subtitle}}", PlaceholderType.Text));
            layouts.Add(defaultLayout);
            totalPlaceholders = 2;
        }

        return (layouts, totalPlaceholders);
    }

    private static IEnumerable<string> ExtractTokens(string xml)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(xml, @"\{\{([a-zA-Z_][a-zA-Z0-9_]*)\}\}");
        return matches.Select(m => m.Value).Distinct();
    }

    private static SlideType DetectSlideType(string name)
    {
        var lower = name.ToLower();
        if (lower.Contains("title") && lower.Contains("sub")) return SlideType.Title;
        if (lower.Contains("bullet") || lower.Contains("content")) return SlideType.Bullet;
        if (lower.Contains("chart") || lower.Contains("graph")) return SlideType.Chart;
        if (lower.Contains("table") || lower.Contains("grid")) return SlideType.Table;
        if (lower.Contains("two") || lower.Contains("split") || lower.Contains("column")) return SlideType.TwoColumn;
        if (lower.Contains("section") || lower.Contains("divider")) return SlideType.SectionBreak;
        return SlideType.Bullet;
    }

    private static PlaceholderType DetectPlaceholderType(string name)
    {
        var lower = name.ToLower();
        foreach (var (key, type) in TypeHeuristics)
            if (lower.Contains(key)) return type;
        return PlaceholderType.Text;
    }
}

// ─── Rule Engine ─────────────────────────────────────────────────────────────
public class RuleEngineService : IRuleEngineService
{
    private static readonly Dictionary<string, string> ExactMatchRules = new()
    {
        { "title", "data.title" }, { "subtitle", "data.subtitle" }, { "heading", "data.heading" },
        { "bullets", "data.bullets" }, { "body", "data.body" }, { "date", "meta.date" },
        { "slide_number", "meta.slideNumber" }, { "logo", "assets.logo" }, { "footer", "meta.footer" },
        { "chart_revenue", "data.revenue" }, { "chart_growth", "data.growth" },
        { "table_data", "data.tableData" }, { "table_metrics", "data.metrics" },
    };

    public List<PlaceholderMapping> AutoMapPlaceholders(IEnumerable<Placeholder> placeholders)
    {
        var result = new List<PlaceholderMapping>();
        foreach (var ph in placeholders)
        {
            var name = ph.Name.ToLower();

            // 1. Exact match
            if (ExactMatchRules.TryGetValue(name, out var field))
            {
                result.Add(new PlaceholderMapping(ph.Id, ph.Token, field, "exact-match", "AutoMapped"));
                continue;
            }

            // 2. Heuristic: startsWith known prefix
            var heuristicMatch = ExactMatchRules.Keys.FirstOrDefault(k => name.StartsWith(k) || name.Contains(k));
            if (heuristicMatch != null)
            {
                result.Add(new PlaceholderMapping(ph.Id, ph.Token, $"data.{name}", "heuristic", "AutoMapped"));
                continue;
            }

            // 3. Unmapped
            result.Add(new PlaceholderMapping(ph.Id, ph.Token, null, "unset", "NeedsReview"));
        }
        return result;
    }
}