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
        string slideJson,
        string outputFileName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(template.ScaffoldPath))
        {
            throw new InvalidOperationException("ScaffoldPath is required for Aspose prompt generation.");
        }

        if (string.IsNullOrWhiteSpace(slideJson))
        {
            throw new InvalidOperationException("SlideJson is required for Aspose PPT generation.");
        }

        var storageBasePath = GetStorageBasePath();
        var scaffoldJsonPath = GetCleanScaffoldJsonFilePath(storageBasePath, template.ScaffoldPath);
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
        var semanticContent = DeserializeSemanticContent(slideJson, "slideJson");
        var normalizedJson = JsonSerializer.Serialize(semanticContent, JsonOptions);

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pptx");

        try
        {
            using (var presentation = new Presentation(templateSourcePath))
            {
                ApplySemanticContent(presentation, scaffold, semanticContent);
                presentation.Save(tempPath, Aspose.Slides.Export.SaveFormat.Pptx);
            }

            await using var stream = File.OpenRead(tempPath);
            var outputPath = await _storage.UploadTemplateAsync(stream, outputFileName);
            return new AsposePromptPptxGenerationResult(outputPath, normalizedJson, semanticContent.Slides.Count);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
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

    private static SemanticContentDocument DeserializeSemanticContent(string json, string description)
    {
        try
        {
            return JsonSerializer.Deserialize<SemanticContentDocument>(json, JsonOptions)
                ?? throw new InvalidOperationException($"{description} was empty.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse {description} JSON.", ex);
        }
    }

    private void ApplySemanticContent(
        Presentation presentation,
        TemplateScaffold scaffold,
        SemanticContentDocument semanticContent)
    {
        var slideCount = Math.Min(presentation.Slides.Count, scaffold.Slides.Count);

        for (var slideIndex = 0; slideIndex < slideCount; slideIndex++)
        {
            var slide = presentation.Slides[slideIndex];
            var scaffoldSlide = scaffold.Slides[slideIndex];
            var semanticSlide = semanticContent.Slides.FirstOrDefault(x => x.SlideNumber == scaffoldSlide.SlideNumber);

            if (semanticSlide is null)
            {
                continue;
            }

            ApplyPlaceholderValues(slide, scaffoldSlide, semanticSlide);
            ApplyChartValues(slide, scaffoldSlide, semanticSlide);
            ApplyTableValues(slide, scaffoldSlide, semanticSlide);
        }
    }

    private void ApplyPlaceholderValues(ISlide slide, TemplateScaffoldSlide scaffoldSlide, SemanticContentSlide semanticSlide)
    {
        var placeholderShapes = slide.Shapes
            .OfType<IAutoShape>()
            .Where(static shape => shape.TextFrame is not null && ShapeContainsPlaceholder(shape))
            .ToList();

        var placeholderCount = Math.Min(scaffoldSlide.Placeholders.Count, placeholderShapes.Count);
        for (var placeholderIndex = 0; placeholderIndex < placeholderCount; placeholderIndex++)
        {
            var scaffoldPlaceholder = scaffoldSlide.Placeholders[placeholderIndex];
            if (!semanticSlide.Placeholders.TryGetValue(scaffoldPlaceholder.Key, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var shape = placeholderShapes[placeholderIndex];
            if (shape.TextFrame is null)
            {
                continue;
            }

            if (string.Equals(scaffoldPlaceholder.Key, "bullets", StringComparison.OrdinalIgnoreCase))
            {
                ReplaceBullets(shape.TextFrame, SplitBullets(value));
            }
            else
            {
                shape.TextFrame.Text = value;
            }

            _logger.LogInformation(
                "Applied semantic placeholder on slide {SlideNumber}: key {PlaceholderKey} -> {PlaceholderValue}",
                scaffoldSlide.SlideNumber,
                scaffoldPlaceholder.Key,
                value);
        }
    }

    private static void ApplyChartValues(ISlide slide, TemplateScaffoldSlide scaffoldSlide, SemanticContentSlide semanticSlide)
    {
        var charts = slide.Shapes.OfType<IChart>().ToList();
        var chartCount = Math.Min(Math.Min(charts.Count, scaffoldSlide.Charts.Count), semanticSlide.Charts.Count);

        for (var chartIndex = 0; chartIndex < chartCount; chartIndex++)
        {
            ApplyChartContent(charts[chartIndex], NormalizeChart(scaffoldSlide.Charts[chartIndex], semanticSlide.Charts[chartIndex]));
        }
    }

    private static void ApplyTableValues(ISlide slide, TemplateScaffoldSlide scaffoldSlide, SemanticContentSlide semanticSlide)
    {
        var tables = slide.Shapes.OfType<ITable>().ToList();
        var tableCount = Math.Min(Math.Min(tables.Count, scaffoldSlide.Tables.Count), semanticSlide.Tables.Count);

        for (var tableIndex = 0; tableIndex < tableCount; tableIndex++)
        {
            ApplyTableContent(tables[tableIndex], NormalizeTable(scaffoldSlide.Tables[tableIndex], semanticSlide.Tables[tableIndex]));
        }
    }

    private static TemplateScaffoldChart NormalizeChart(TemplateScaffoldChart scaffoldChart, SemanticContentChart semanticChart)
    {
        var normalized = new TemplateScaffoldChart
        {
            ChartIndex = scaffoldChart.ChartIndex,
            ChartType = string.IsNullOrWhiteSpace(semanticChart.ChartType) ? scaffoldChart.ChartType : semanticChart.ChartType,
            Categories = semanticChart.Categories.Count > 0 ? semanticChart.Categories : scaffoldChart.Categories
        };

        for (var seriesIndex = 0; seriesIndex < scaffoldChart.Series.Count; seriesIndex++)
        {
            var semanticSeries = seriesIndex < semanticChart.Series.Count
                ? semanticChart.Series[seriesIndex]
                : new SemanticContentSeries();

            normalized.Series.Add(new TemplateScaffoldSeries
            {
                Name = string.IsNullOrWhiteSpace(semanticSeries.Name) ? scaffoldChart.Series[seriesIndex].Name : semanticSeries.Name,
                Values = semanticSeries.Data.Count > 0 ? semanticSeries.Data : scaffoldChart.Series[seriesIndex].Values
            });
        }

        return normalized;
    }

    private static TemplateScaffoldTable NormalizeTable(TemplateScaffoldTable scaffoldTable, SemanticContentTable semanticTable)
    {
        return new TemplateScaffoldTable
        {
            RowCount = semanticTable.Rows > 0 ? semanticTable.Rows : scaffoldTable.RowCount,
            ColumnCount = semanticTable.Columns > 0 ? semanticTable.Columns : scaffoldTable.ColumnCount,
            Rows = semanticTable.Data.Count > 0 ? semanticTable.Data : scaffoldTable.Rows
        };
    }

    private static void ApplyChartContent(IChart chart, TemplateScaffoldChart chartContent)
    {
        var chartData = chart.ChartData;

        // ✅ DO NOT CLEAR → preserve formatting
        if (chartData.Series.Count == 0)
            return;

        var series = chartData.Series[0];

        var categoryCount = Math.Min(chartData.Categories.Count, chartContent.Categories.Count);
        var valueCount = Math.Min(series.DataPoints.Count, chartContent.Series[0].Values.Count);

        // ✅ Update categories (labels)
        for (int i = 0; i < categoryCount; i++)
        {
            chartData.Categories[i].Value = chartContent.Categories[i];
        }

        // ✅ Update values (THIS preserves color per slice)
        for (int i = 0; i < valueCount; i++)
        {
            var numericValue = ParseNumericValue(chartContent.Series[0].Values[i]);

            var point = series.DataPoints[i];

            if (chart.Type == ChartType.Pie || chart.Type == ChartType.ExplodedPie)
            {
                point.Value.AsCell.Value = numericValue;
            }
            else
            {
                point.Value.AsCell.Value = numericValue;
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

    private static bool ShapeContainsPlaceholder(IAutoShape shape)
    {
        if (shape.TextFrame is null)
        {
            return false;
        }

        return ReadShapeText(shape).Contains("{{", StringComparison.Ordinal);
    }

    private static string ReadShapeText(IAutoShape shape)
    {
        if (shape.TextFrame is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var paragraph in shape.TextFrame.Paragraphs)
        {
            foreach (var portion in paragraph.Portions)
            {
                if (!string.IsNullOrEmpty(portion.Text))
                {
                    builder.Append(portion.Text);
                }
            }
        }

        return builder.ToString();
    }

    private static List<string> SplitBullets(string value)
    {
        return value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static x => x.Trim())
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToList();
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

    private static string GetCleanScaffoldJsonFilePath(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetArtifactsDirectory(storageBasePath, scaffoldPath), "clean_template.scaffold.json");

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

    private class SemanticContentDocument
    {
        public List<SemanticContentSlide> Slides { get; set; } = new();
    }

    private class SemanticContentSlide
    {
        public int SlideNumber { get; set; }
        public Dictionary<string, string> Placeholders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SemanticContentChart> Charts { get; set; } = new();
        public List<SemanticContentTable> Tables { get; set; } = new();
    }

    private class SemanticContentChart
    {
        public int ChartIndex { get; set; }
        public string ChartType { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new();
        public List<SemanticContentSeries> Series { get; set; } = new();
    }

    private class SemanticContentSeries
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Data { get; set; } = new();
    }

    private class SemanticContentTable
    {
        public int TableIndex { get; set; }
        public int Rows { get; set; }
        public int Columns { get; set; }
        public List<List<string>> Data { get; set; } = new();
    }
}
