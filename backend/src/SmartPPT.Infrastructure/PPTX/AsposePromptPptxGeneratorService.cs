using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPPT.Domain.Interfaces;
using SmartPPT.Infrastructure.Models;
using Template = SmartPPT.Domain.Entities.Template;

namespace SmartPPT.Infrastructure.PPTX;

public class AsposePromptPptxGeneratorService : IAsposePromptPptxGeneratorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IStorageService _storage;
    private readonly ILogger<AsposePromptPptxGeneratorService> _logger;

    public AsposePromptPptxGeneratorService(
        IConfiguration configuration,
        IHttpClientFactory httpFactory,
        IStorageService storage,
        ILogger<AsposePromptPptxGeneratorService> logger)
    {
        _configuration = configuration;
        _httpFactory = httpFactory;
        _storage = storage;
        _logger = logger;
    }

    public async Task<AsposePromptPptxGenerationResult> GenerateAsync(
        Template template,
        string userMessage,
        string outputFileName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(template.ScaffoldPath))
        {
            throw new InvalidOperationException("ScaffoldPath is required for Aspose prompt generation.");
        }

        var storageBasePath = GetStorageBasePath();
        var scaffoldJsonPath = GetScaffoldJsonFilePath(storageBasePath, template.ScaffoldPath);
        var templateSourcePath = ResolveTemplateSourceFilePath(storageBasePath, template.ScaffoldPath);

        if (!File.Exists(scaffoldJsonPath))
        {
            throw new FileNotFoundException($"Template scaffold JSON not found at {scaffoldJsonPath}", scaffoldJsonPath);
        }

        if (!File.Exists(templateSourcePath))
        {
            throw new FileNotFoundException($"Template source PPTX not found at {templateSourcePath}", templateSourcePath);
        }

        var scaffoldJson = await File.ReadAllTextAsync(scaffoldJsonPath, ct);
        var scaffold = DeserializeScaffold(scaffoldJson, "template scaffold");

        var llmJson = await GenerateContentJsonAsync(userMessage, scaffoldJson, ct);
        var generated = DeserializeScaffold(llmJson, "LLM response");
        var normalized = NormalizeGeneratedScaffold(scaffold, generated);
        var normalizedJson = JsonSerializer.Serialize(normalized, JsonOptions);

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pptx");

        try
        {
            using (var presentation = new Presentation(templateSourcePath))
            {
                ApplyContent(presentation, normalized);
                presentation.Save(tempPath, Aspose.Slides.Export.SaveFormat.Pptx);
            }

            await using var stream = File.OpenRead(tempPath);
            var outputPath = await _storage.UploadTemplateAsync(stream, outputFileName);
            return new AsposePromptPptxGenerationResult(outputPath, normalizedJson, normalized.Slides.Count);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private async Task<string> GenerateContentJsonAsync(string userMessage, string scaffoldJson, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_configuration["AI:OpenRouter:ApiKey"]))
        {
            return await CallOpenRouterAsync(userMessage, scaffoldJson, ct);
        }

        if (!string.IsNullOrWhiteSpace(_configuration["AI:AzureOpenAI:ApiKey"]) &&
            !string.IsNullOrWhiteSpace(_configuration["AI:AzureOpenAI:Endpoint"]))
        {
            return await CallAzureOpenAiAsync(userMessage, scaffoldJson, ct);
        }

        throw new InvalidOperationException("No AI provider is configured for Aspose prompt generation.");
    }

    private async Task<string> CallOpenRouterAsync(string userMessage, string scaffoldJson, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("OpenRouter");
        var apiKey = _configuration["AI:OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException("Missing AI:OpenRouter:ApiKey configuration.");
        var model = _configuration["AI:OpenRouter:DefaultModel"] ?? "google/gemma-3n-e2b-it:free";

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        client.DefaultRequestHeaders.Add("HTTP-Referer", "https://smartppt.local");

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = BuildUserPrompt(userMessage, scaffoldJson) }
            },
            temperature = 0.3,
            max_tokens = 4000
        };

        var response = await client.PostAsJsonAsync("https://openrouter.ai/api/v1/chat/completions", requestBody, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(responseBody);

        var content = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return ExtractJson(content);
    }

    private async Task<string> CallAzureOpenAiAsync(string userMessage, string scaffoldJson, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("AzureOpenAI");
        var endpoint = _configuration["AI:AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("Missing AI:AzureOpenAI:Endpoint configuration.");
        var apiKey = _configuration["AI:AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("Missing AI:AzureOpenAI:ApiKey configuration.");
        var deployment = _configuration["AI:AzureOpenAI:DefaultDeployment"] ?? "gpt-4o";
        var apiVersion = _configuration["AI:AzureOpenAI:ApiVersion"] ?? "2024-02-01";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}");

        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                messages = new[]
                {
                    new { role = "system", content = GetSystemPrompt() },
                    new { role = "user", content = BuildUserPrompt(userMessage, scaffoldJson) }
                },
                temperature = 0.3,
                max_tokens = 4000
            }),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(responseBody);

        var content = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return ExtractJson(content);
    }

    private static string GetSystemPrompt() => """
        You generate presentation content JSON for a PowerPoint template scaffold.
        Return ONLY valid JSON with the exact same object structure and property names as the scaffold.
        Keep the same number of slides.
        Keep each slide's SlideNumber and SlideType aligned to the scaffold.
        Fill TitleText with presentation text.
        Fill BulletPlaceholders with final bullet strings.
        Fill Charts with categories, series names, and series values.
        Fill Tables with rows and cell text.
        Do not add markdown, explanations, or extra properties.
        """;

    private static string BuildUserPrompt(string userMessage, string scaffoldJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("User request:");
        builder.AppendLine(userMessage);
        builder.AppendLine();
        builder.AppendLine("Template scaffold JSON:");
        builder.AppendLine(scaffoldJson);
        builder.AppendLine();
        builder.AppendLine("Return JSON only.");
        return builder.ToString();
    }

    private static string ExtractJson(string? rawContent)
    {
        var content = rawContent?.Trim() ?? string.Empty;

        if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            content = content[7..];
        }
        else if (content.StartsWith("```", StringComparison.Ordinal))
        {
            content = content[3..];
        }

        if (content.EndsWith("```", StringComparison.Ordinal))
        {
            content = content[..^3];
        }

        return content.Trim();
    }

    private static TemplateScaffold DeserializeScaffold(string json, string description)
    {
        try
        {
            return JsonSerializer.Deserialize<TemplateScaffold>(json, JsonOptions)
                ?? throw new InvalidOperationException($"{description} was empty.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse {description} JSON.", ex);
        }
    }

    private static TemplateScaffold NormalizeGeneratedScaffold(TemplateScaffold scaffold, TemplateScaffold generated)
    {
        var normalized = new TemplateScaffold
        {
            TemplateName = string.IsNullOrWhiteSpace(generated.TemplateName)
                ? scaffold.TemplateName
                : generated.TemplateName
        };

        for (var i = 0; i < scaffold.Slides.Count; i++)
        {
            var scaffoldSlide = scaffold.Slides[i];
            var generatedSlide = i < generated.Slides.Count ? generated.Slides[i] : new TemplateScaffoldSlide();

            normalized.Slides.Add(new TemplateScaffoldSlide
            {
                SlideNumber = scaffoldSlide.SlideNumber,
                SlideType = string.IsNullOrWhiteSpace(generatedSlide.SlideType) ? scaffoldSlide.SlideType : generatedSlide.SlideType,
                TitleText = string.IsNullOrWhiteSpace(generatedSlide.TitleText) ? scaffoldSlide.TitleText : generatedSlide.TitleText,
                BulletPlaceholders = generatedSlide.BulletPlaceholders.Count > 0
                    ? generatedSlide.BulletPlaceholders.Where(static x => !string.IsNullOrWhiteSpace(x)).ToList()
                    : scaffoldSlide.BulletPlaceholders.ToList(),
                Charts = NormalizeCharts(scaffoldSlide.Charts, generatedSlide.Charts),
                Tables = NormalizeTables(scaffoldSlide.Tables, generatedSlide.Tables)
            });
        }

        return normalized;
    }

    private static List<TemplateScaffoldChart> NormalizeCharts(
        List<TemplateScaffoldChart> scaffoldCharts,
        List<TemplateScaffoldChart> generatedCharts)
    {
        var normalized = new List<TemplateScaffoldChart>();

        for (var i = 0; i < scaffoldCharts.Count; i++)
        {
            var scaffoldChart = scaffoldCharts[i];
            var generatedChart = i < generatedCharts.Count ? generatedCharts[i] : new TemplateScaffoldChart();

            normalized.Add(new TemplateScaffoldChart
            {
                ChartType = string.IsNullOrWhiteSpace(generatedChart.ChartType) ? scaffoldChart.ChartType : generatedChart.ChartType,
                Categories = generatedChart.Categories.Count > 0 ? generatedChart.Categories : scaffoldChart.Categories,
                Series = NormalizeSeries(scaffoldChart.Series, generatedChart.Series)
            });
        }

        return normalized;
    }

    private static List<TemplateScaffoldSeries> NormalizeSeries(
        List<TemplateScaffoldSeries> scaffoldSeries,
        List<TemplateScaffoldSeries> generatedSeries)
    {
        var normalized = new List<TemplateScaffoldSeries>();

        for (var i = 0; i < scaffoldSeries.Count; i++)
        {
            var scaffoldItem = scaffoldSeries[i];
            var generatedItem = i < generatedSeries.Count ? generatedSeries[i] : new TemplateScaffoldSeries();

            normalized.Add(new TemplateScaffoldSeries
            {
                Name = string.IsNullOrWhiteSpace(generatedItem.Name) ? scaffoldItem.Name : generatedItem.Name,
                Values = generatedItem.Values.Count > 0 ? generatedItem.Values : scaffoldItem.Values
            });
        }

        return normalized;
    }

    private static List<TemplateScaffoldTable> NormalizeTables(
        List<TemplateScaffoldTable> scaffoldTables,
        List<TemplateScaffoldTable> generatedTables)
    {
        var normalized = new List<TemplateScaffoldTable>();

        for (var i = 0; i < scaffoldTables.Count; i++)
        {
            var scaffoldTable = scaffoldTables[i];
            var generatedTable = i < generatedTables.Count ? generatedTables[i] : new TemplateScaffoldTable();

            normalized.Add(new TemplateScaffoldTable
            {
                RowCount = generatedTable.RowCount > 0 ? generatedTable.RowCount : scaffoldTable.RowCount,
                ColumnCount = generatedTable.ColumnCount > 0 ? generatedTable.ColumnCount : scaffoldTable.ColumnCount,
                Rows = generatedTable.Rows.Count > 0 ? generatedTable.Rows : scaffoldTable.Rows
            });
        }

        return normalized;
    }

    private void ApplyContent(Presentation presentation, TemplateScaffold content)
    {
        var slideCount = Math.Min(presentation.Slides.Count, content.Slides.Count);

        for (var slideIndex = 0; slideIndex < slideCount; slideIndex++)
        {
            var slide = presentation.Slides[slideIndex];
            var contentSlide = content.Slides[slideIndex];

            var titleApplied = false;
            var bulletsApplied = false;
            var chartIndex = 0;
            var tableIndex = 0;

            foreach (var shape in slide.Shapes)
            {
                switch (shape)
                {
                    case IAutoShape autoShape:
                        ApplyTextContent(autoShape, contentSlide, ref titleApplied, ref bulletsApplied);
                        break;
                    case IChart chart when chartIndex < contentSlide.Charts.Count:
                        ApplyChartContent(chart, contentSlide.Charts[chartIndex++]);
                        break;
                    case ITable table when tableIndex < contentSlide.Tables.Count:
                        ApplyTableContent(table, contentSlide.Tables[tableIndex++]);
                        break;
                }
            }
        }
    }

    private static void ApplyTextContent(
        IAutoShape shape,
        TemplateScaffoldSlide contentSlide,
        ref bool titleApplied,
        ref bool bulletsApplied)
    {
        if (shape.TextFrame is null)
        {
            return;
        }

        if (!titleApplied && !string.IsNullOrWhiteSpace(contentSlide.TitleText) && IsTitleShape(shape))
        {
            shape.TextFrame.Text = contentSlide.TitleText;
            titleApplied = true;
            return;
        }

        if (!bulletsApplied && contentSlide.BulletPlaceholders.Count > 0 && IsBulletShape(shape))
        {
            ReplaceBullets(shape.TextFrame, contentSlide.BulletPlaceholders);
            bulletsApplied = true;
            return;
        }

        foreach (var paragraph in shape.TextFrame.Paragraphs)
        {
            if (paragraph.Portions.Count == 0)
            {
                continue;
            }

            foreach (var portion in paragraph.Portions)
            {
                var text = portion.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(contentSlide.TitleText))
                {
                    text = ReplaceTitleTokens(text, contentSlide.TitleText);
                }

                if (contentSlide.BulletPlaceholders.Count > 0)
                {
                    text = ReplaceBulletTokens(text, contentSlide.BulletPlaceholders);
                }

                portion.Text = text;
            }
        }
    }

    private static bool IsTitleShape(IAutoShape shape)
    {
        var placeholderType = shape.Placeholder?.Type;
        if (placeholderType == PlaceholderType.Title || placeholderType == PlaceholderType.CenteredTitle)
        {
            return true;
        }

        return shape.TextFrame?.Text?.Contains("{{", StringComparison.Ordinal) == true;
    }

    private static bool IsBulletShape(IAutoShape shape)
    {
        if (shape.TextFrame is null)
        {
            return false;
        }

        if (shape.TextFrame.Text.Contains("{{bullet", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return shape.TextFrame.Paragraphs.Any(p => p.ParagraphFormat?.Bullet?.Type != BulletType.None);
    }

    private static void ReplaceBullets(ITextFrame textFrame, IReadOnlyList<string> bullets)
    {
        textFrame.Paragraphs.Clear();

        foreach (var bullet in bullets)
        {
            var paragraph = new Paragraph();
            paragraph.ParagraphFormat.Bullet.Type = BulletType.Symbol;
            paragraph.Portions.Add(new Portion(bullet));
            textFrame.Paragraphs.Add(paragraph);
        }
    }

    private static void ApplyChartContent(IChart chart, TemplateScaffoldChart chartContent)
    {
        var workbook = chart.ChartData.ChartDataWorkbook;
        chart.ChartData.Series.Clear();
        chart.ChartData.Categories.Clear();

        for (var categoryIndex = 0; categoryIndex < chartContent.Categories.Count; categoryIndex++)
        {
            chart.ChartData.Categories.Add(workbook.GetCell(0, categoryIndex + 1, 0, chartContent.Categories[categoryIndex]));
        }

        for (var seriesIndex = 0; seriesIndex < chartContent.Series.Count; seriesIndex++)
        {
            var seriesContent = chartContent.Series[seriesIndex];
            var series = chart.ChartData.Series.Add(
                workbook.GetCell(0, 0, seriesIndex + 1, seriesContent.Name),
                chart.Type);

            for (var valueIndex = 0; valueIndex < seriesContent.Values.Count; valueIndex++)
            {
                var numericValue = ParseNumericValue(seriesContent.Values[valueIndex]);
                AddDataPoint(series, chart.Type, workbook, valueIndex + 1, seriesIndex + 1, numericValue);
            }
        }
    }

    private static void ApplyTableContent(ITable table, TemplateScaffoldTable tableContent)
    {
        var rowCount = Math.Min(table.Rows.Count, tableContent.Rows.Count);

        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var sourceRow = tableContent.Rows[rowIndex];
            var columnCount = Math.Min(table.Columns.Count, sourceRow.Count);

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                table.Rows[rowIndex][columnIndex].TextFrame.Text = sourceRow[columnIndex] ?? string.Empty;
            }
        }
    }

    private static void AddDataPoint(
        IChartSeries series,
        ChartType chartType,
        IChartDataWorkbook workbook,
        int row,
        int column,
        double value)
    {
        var cell = workbook.GetCell(0, row, column, value);

        if (chartType == ChartType.Pie || chartType == ChartType.ExplodedPie)
        {
            series.DataPoints.AddDataPointForPieSeries(cell);
            return;
        }

        if (chartType == ChartType.Doughnut || chartType == ChartType.ExplodedDoughnut)
        {
            series.DataPoints.AddDataPointForDoughnutSeries(cell);
            return;
        }

        if (chartType == ChartType.Line || chartType == ChartType.LineWithMarkers)
        {
            series.DataPoints.AddDataPointForLineSeries(cell);
            return;
        }

        series.DataPoints.AddDataPointForBarSeries(cell);
    }

    private static double ParseNumericValue(string? rawValue)
    {
        return double.TryParse(rawValue, out var value) ? value : 0d;
    }

    private static string ReplaceTitleTokens(string text, string title)
    {
        return text
            .Replace("{{title}}", title, StringComparison.OrdinalIgnoreCase)
            .Replace("{{heading}}", title, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceBulletTokens(string text, IReadOnlyList<string> bullets)
    {
        var output = text;
        for (var i = 0; i < bullets.Count; i++)
        {
            output = output.Replace($"{{{{bullet_{i + 1}}}}}", bullets[i], StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }

    private string GetStorageBasePath()
    {
        return _configuration["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "smartppt");
    }

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

    private static string GetArtifactsDirectory(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetTemplateRoot(storageBasePath, scaffoldPath), "artifacts");

    private static string GetSourceDirectory(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetTemplateRoot(storageBasePath, scaffoldPath), "source");

    private static string GetScaffoldJsonFilePath(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetArtifactsDirectory(storageBasePath, scaffoldPath), "template.scaffold.json");

    private static string ResolveTemplateSourceFilePath(string storageBasePath, string scaffoldPath)
    {
        var sourceDirectory = GetSourceDirectory(storageBasePath, scaffoldPath);
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*.pptx", SearchOption.TopDirectoryOnly);
        if (sourceFiles.Length == 0)
        {
            throw new FileNotFoundException($"No template source file found under {sourceDirectory}", sourceDirectory);
        }

        return sourceFiles[0];
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
}
