using System.Text;
using System.Text.Json;
using Aspose.Slides;
using Aspose.Slides.Charts;
using SmartPPT.Infrastructure.Models;

namespace SmartPPT.Infrastructure.Services;

public interface ITemplateScaffoldGeneratorService
{
    Task<TemplateScaffoldArtifacts> GenerateAsync(string pptxFilePath, string templateName, CancellationToken ct = default);
}

public class AsposeTemplateScaffoldService : ITemplateScaffoldGeneratorService
{
    public Task<TemplateScaffoldArtifacts> GenerateAsync(string pptxFilePath, string templateName, CancellationToken ct = default)
    {
        _ = ct;

        if (!File.Exists(pptxFilePath))
        {
            throw new FileNotFoundException($"Template PPTX file not found at {pptxFilePath}", pptxFilePath);
        }

        try
        {
            using var presentation = new Presentation(pptxFilePath);
            var scaffold = new TemplateScaffold
            {
                TemplateName = templateName
            };

            for (var i = 0; i < presentation.Slides.Count; i++)
            {
                scaffold.Slides.Add(BuildSlideScaffold(presentation.Slides[i], i + 1));
            }

            var json = JsonSerializer.Serialize(scaffold, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return Task.FromResult(new TemplateScaffoldArtifacts(json, BuildScript(scaffold)));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate scaffold from template '{templateName}'.", ex);
        }
    }

    private static TemplateScaffoldSlide BuildSlideScaffold(ISlide slide, int slideNumber)
    {
        var titleText = string.Empty;
        var bullets = new List<string>();
        var charts = new List<TemplateScaffoldChart>();
        var tables = new List<TemplateScaffoldTable>();

        foreach (var shape in slide.Shapes)
        {
            try
            {
                switch (shape)
                {
                    case IAutoShape autoShape:
                        ExtractText(autoShape, ref titleText, bullets);
                        break;
                    case IChart chart:
                        charts.Add(ExtractChart(chart));
                        break;
                    case ITable table:
                        tables.Add(ExtractTable(table));
                        break;
                }
            }
            catch
            {
                // Missing or malformed shapes are ignored so scaffold generation remains resilient.
            }
        }

        return new TemplateScaffoldSlide
        {
            SlideNumber = slideNumber,
            SlideType = ResolveSlideType(charts.Count, tables.Count, bullets.Count),
            TitleText = string.IsNullOrWhiteSpace(titleText) ? null : titleText,
            BulletPlaceholders = bullets,
            Charts = charts,
            Tables = tables
        };
    }

    private static void ExtractText(IAutoShape autoShape, ref string titleText, List<string> bullets)
    {
        if (autoShape.TextFrame is null)
        {
            return;
        }

        var placeholderType = autoShape.Placeholder?.Type;
        var paragraphs = autoShape.TextFrame.Paragraphs;
        foreach (var paragraph in paragraphs)
        {
            var text = paragraph.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var isBullet = paragraph.ParagraphFormat?.Bullet?.Type != BulletType.None;
            if (isBullet)
            {
                bullets.Add(text);
                continue;
            }

            if (string.IsNullOrWhiteSpace(titleText) &&
                (placeholderType == PlaceholderType.Title || placeholderType == PlaceholderType.CenteredTitle))
            {
                titleText = text;
                continue;
            }

            if (string.IsNullOrWhiteSpace(titleText))
            {
                titleText = text;
            }
        }
    }

    private static TemplateScaffoldChart ExtractChart(IChart chart)
    {
        var scaffold = new TemplateScaffoldChart
        {
            ChartType = chart.Type.ToString()
        };

        foreach (IChartCategory category in chart.ChartData.Categories)
        {
            scaffold.Categories.Add(category.Value?.ToString() ?? string.Empty);
        }

        foreach (IChartSeries series in chart.ChartData.Series)
        {
            var scaffoldSeries = new TemplateScaffoldSeries
            {
                Name = series.Name?.AsLiteralString
                    ?? series.Name?.ToString()
                    ?? "Series"
            };

            foreach (IChartDataPoint point in series.DataPoints)
            {
                scaffoldSeries.Values.Add(point.Value?.Data?.ToString() ?? string.Empty);
            }

            scaffold.Series.Add(scaffoldSeries);
        }

        return scaffold;
    }

    private static TemplateScaffoldTable ExtractTable(ITable table)
    {
        var rows = new List<List<string>>();
        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            var cells = new List<string>();
            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                var cell = row[columnIndex];
                cells.Add(cell.TextFrame?.Text?.Trim() ?? string.Empty);
            }
            rows.Add(cells);
        }

        return new TemplateScaffoldTable
        {
            RowCount = table.Rows.Count,
            ColumnCount = table.Columns.Count,
            Rows = rows
        };
    }

    private static string ResolveSlideType(int chartCount, int tableCount, int bulletCount)
    {
        if (chartCount > 0) return "Chart";
        if (tableCount > 0) return "Table";
        if (bulletCount > 0) return "Bullet";
        return "Title";
    }

    private static string BuildScript(TemplateScaffold scaffold)
    {
        var builder = new StringBuilder();
        builder.AppendLine("const templateScaffold = {");
        builder.AppendLine($"  templateName: {JsonSerializer.Serialize(scaffold.TemplateName)},");
        builder.AppendLine("  slides: [");

        for (var i = 0; i < scaffold.Slides.Count; i++)
        {
            var slide = scaffold.Slides[i];
            var bulletPlaceholders = slide.BulletPlaceholders.Count == 0
                ? new List<string> { "{{bullet_1}}", "{{bullet_2}}" }
                : slide.BulletPlaceholders;
            builder.AppendLine("    {");
            builder.AppendLine($"      slideNumber: {slide.SlideNumber},");
            builder.AppendLine($"      slideType: {JsonSerializer.Serialize(slide.SlideType)},");
            builder.AppendLine($"      title: {JsonSerializer.Serialize(slide.TitleText ?? "{{title}}")},");
            builder.AppendLine($"      bullets: {JsonSerializer.Serialize(bulletPlaceholders)},");
            builder.AppendLine($"      charts: {JsonSerializer.Serialize(slide.Charts)},");
            builder.AppendLine($"      tables: {JsonSerializer.Serialize(slide.Tables)}");
            builder.Append("    }");
            builder.AppendLine(i < scaffold.Slides.Count - 1 ? "," : string.Empty);
        }

        builder.AppendLine("  ]");
        builder.AppendLine("};");
        builder.AppendLine();
        builder.AppendLine("module.exports = templateScaffold;");
        return builder.ToString();
    }
}
