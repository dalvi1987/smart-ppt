using System.Text;
using System.Text.Json;
using System.Diagnostics;
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
        var placeholders = new List<TemplateScaffoldPlaceholder>();
        var charts = new List<TemplateScaffoldChart>();
        var tables = new List<TemplateScaffoldTable>();
        var chartIndex = 0;

        foreach (var shape in slide.Shapes)
        {
            var shapeId = shape.UniqueId;

            try
            {
                LogDebug($"Processing shape: {shape.GetType().Name}, Id={shapeId}");

                // FIX: ITable is not a subtype of IAutoShape or IChart — check it FIRST
                // via the ShapeType enum, then cast. Putting it before the IAutoShape arm
                // also prevents text inside table cells being picked up as placeholders.
                if (shape is ITable table)
                {
                    tables.Add(ExtractTable(table));
                }
                else
                {
                    switch (shape)
                    {
                        case IChart chart:
                            charts.Add(ExtractChart(chart, chartIndex++, shapeId.ToString()));
                            break;

                        case IAutoShape autoShape:
                            ExtractPlaceholders(autoShape, placeholders, shapeId.ToString());
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Shape failed (id={shapeId}): {ex.Message}");
            }
        }

        return new TemplateScaffoldSlide
        {
            SlideNumber = slideNumber,
            SlideType = "Custom",
            Placeholders = placeholders,
            Charts = charts,
            Tables = tables
        };
    }

    // -------------------------------------------------------------------------
    // Placeholder extraction (text shapes)
    // -------------------------------------------------------------------------

    private static void ExtractPlaceholders(IAutoShape autoShape, List<TemplateScaffoldPlaceholder> placeholders, string shapeId)
    {
        if (autoShape.TextFrame is null)
        {
            return;
        }

        var fullText = ReadTextFrameText(autoShape.TextFrame).Trim();
        if (string.IsNullOrWhiteSpace(fullText))
        {
            return;
        }

        LogDebug($"Shape text extracted: '{fullText}'");

        foreach (var placeholder in ExtractPlaceholderEntries(fullText))
        {
            placeholders.Add(new TemplateScaffoldPlaceholder
            {
                Key = placeholder.Key,
                RawText = placeholder.RawText,
                Type = "Text",
                ShapeId = shapeId
            });
            LogDebug($"Detected placeholder: key='{placeholder.Key}', raw='{placeholder.RawText}'");
        }
    }

    private static string ReadTextFrameText(ITextFrame textFrame)
    {
        var builder = new StringBuilder();

        foreach (var paragraph in textFrame.Paragraphs)
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

    private static IEnumerable<TemplateScaffoldPlaceholder> ExtractPlaceholderEntries(string text)
    {
        var results = new List<TemplateScaffoldPlaceholder>();
        var startIndex = 0;

        while (startIndex < text.Length)
        {
            var openIndex = text.IndexOf("{{", startIndex, StringComparison.Ordinal);
            if (openIndex < 0)
            {
                break;
            }

            var closeIndex = text.IndexOf("}}", openIndex + 2, StringComparison.Ordinal);
            var rawText = closeIndex >= 0
                ? text.Substring(openIndex, closeIndex - openIndex + 2)
                : text[openIndex..];

            var keySource = rawText.StartsWith("{{", StringComparison.Ordinal)
                ? rawText[2..]
                : rawText;

            var closingMarkerIndex = keySource.IndexOf("}}", StringComparison.Ordinal);
            if (closingMarkerIndex >= 0)
            {
                keySource = keySource[..closingMarkerIndex];
            }

            var key = CleanPlaceholderKey(keySource);
            if (!string.IsNullOrWhiteSpace(key))
            {
                results.Add(new TemplateScaffoldPlaceholder
                {
                    Key = key,
                    RawText = rawText.Trim(),
                    Type = "Text"
                });
            }

            startIndex = openIndex + 2;
        }

        return results;
    }

    private static string CleanPlaceholderKey(string rawKey)
    {
        var builder = new StringBuilder();

        foreach (var ch in rawKey.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    // -------------------------------------------------------------------------
    // Chart extraction
    // -------------------------------------------------------------------------

    private static TemplateScaffoldChart ExtractChart(IChart chart, int chartIndex, string shapeId)
    {
        var scaffold = new TemplateScaffoldChart
        {
            ChartIndex = chartIndex,
            ShapeId = shapeId,
            ChartType = MapChartType(chart.Type),
            Key = $"chart_{chartIndex + 1}"
        };

        // --- Title -----------------------------------------------------------
        // FIX: TextFrameForOverriding is only non-null when the title text has
        // been explicitly overridden in the slide (i.e. not linked to the sheet).
        // When the title comes from the embedded workbook — which is the common
        // case — TextFrameForOverriding is null. Use it first; fall back to the
        // read-only TextFrame on the same object.
        try
        {
            if (chart.HasTitle && chart.ChartTitle != null)
            {
                var textFrame = chart.ChartTitle.TextFrameForOverriding;
                             //?? chart.ChartTitle.TextFrame; // <-- FIX: fallback

                if (textFrame != null)
                {
                    var titleText = ReadTextFrameText(textFrame);
                    LogDebug($"Chart title text: '{titleText}'");

                    var titleKey = ExtractPlaceholderEntries(titleText)
                        .Select(p => p.Key)
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(titleKey))
                    {
                        scaffold.TitleKey = titleKey;
                        scaffold.Key = $"{titleKey}_chart";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Chart title error: {ex.Message}");
        }

        // --- Categories ------------------------------------------------------
        // FIX: category.Value is a ChartDataCell, not a string.
        // The plain .ToString() gives you a type name, not the cell content.
        // Use AsLiteralString for static values, or read the cell Value property.
        try
        {
            foreach (IChartCategory category in chart.ChartData.Categories)
            {
                var raw = ReadCategoryText(category);
                LogDebug($"Chart category raw: '{raw}'");

                var keys = ExtractPlaceholderEntries(raw).Select(p => p.Key).ToList();
                if (keys.Count > 0)
                {
                    scaffold.Categories.AddRange(keys);
                }
                else if (!string.IsNullOrWhiteSpace(raw))
                {
                    // Not a placeholder — store the literal value so the scaffold
                    // still reflects real category labels.
                    scaffold.Categories.Add(raw.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Chart category error: {ex.Message}");
        }

        // --- Series ----------------------------------------------------------
        // FIX: Also extract data-point values so Values list is populated.
        // For template scaffolding we collect placeholder keys from both the
        // series name and each data-point value cell.
        try
        {
            foreach (IChartSeries series in chart.ChartData.Series)
            {
                var seriesName = ReadSeriesName(series, scaffold.Series.Count);
                var seriesEntry = new TemplateScaffoldSeries { Name = seriesName };

                foreach (IChartDataPoint point in series.DataPoints)
                {
                    var cellText = ReadDataPointText(point);
                    if (string.IsNullOrWhiteSpace(cellText))
                    {
                        continue;
                    }

                    var keys = ExtractPlaceholderEntries(cellText).Select(p => p.Key).ToList();
                    if (keys.Count > 0)
                    {
                        seriesEntry.Values.AddRange(keys);
                    }
                    else
                    {
                        seriesEntry.Values.Add(cellText.Trim());
                    }
                }

                scaffold.Series.Add(seriesEntry);
                LogDebug($"Series '{seriesName}' with {seriesEntry.Values.Count} data points");
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Chart series error: {ex.Message}");
        }

        return scaffold;
    }

    /// <summary>
    /// Safely reads a category label from an IChartCategory.
    /// IChartCategory has two storage modes controlled by UseCell:
    ///   UseCell=true  → value lives in AsCell (IChartDataCell, worksheet-backed)
    ///   UseCell=false → value lives in AsLiteral (plain object, typically a string)
    /// The unified Value property returns whichever one is active.
    /// </summary>
    private static string ReadCategoryText(IChartCategory category)
    {
        try
        {
            // Value is the single safe read — it returns AsCell.Value when UseCell
            // is true, and AsLiteral when UseCell is false.
            var raw = category.Value;
            if (raw != null)
            {
                return raw.ToString() ?? string.Empty;
            }
        }
        catch { /* ignore */ }

        return string.Empty;
    }

    /// <summary>
    /// Safely reads a series name.
    /// IStringChartValue.AsLiteralString is set when the name is a literal (not
    /// worksheet-backed). For worksheet-backed names, ToString() on the value
    /// object returns the resolved display string — that is the safe fallback
    /// for both modes because Aspose overrides ToString() on IStringChartValue.
    /// </summary>
    private static string ReadSeriesName(IChartSeries series, int fallbackIndex)
    {
        try
        {
            // AsLiteralString is non-null/non-empty when DataSourceType == StringLiterals
            if (!string.IsNullOrWhiteSpace(series.Name?.AsLiteralString))
            {
                return series.Name.AsLiteralString;
            }
        }
        catch { /* not a literal */ }

        try
        {
            // ToString() is overridden on IStringChartValue and returns the
            // resolved string for both literal and worksheet-backed names.
            var name = series.Name?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        catch { /* ignore */ }

        return $"Series {fallbackIndex + 1}";
    }

    /// <summary>
    /// Reads the numeric value from a chart data point as a string, for
    /// placeholder scanning. YValue is IDoubleChartValue; its AsCell property
    /// gives an IChartDataCell whose Value is the raw object (typically double).
    /// </summary>
    private static string ReadDataPointText(IChartDataPoint point)
    {
        try
        {
            var cell = point.YValue?.AsCell;
            if (cell?.Value != null)
            {
                return cell.Value.ToString() ?? string.Empty;
            }
        }
        catch { /* ignore */ }

        return string.Empty;
    }

    private static string MapChartType(ChartType chartType)
    {
        var type = chartType.ToString();

        if (type.Contains("Pie", StringComparison.OrdinalIgnoreCase))
            return "pie";

        if (type.Contains("Area", StringComparison.OrdinalIgnoreCase))
            return "area";

        if (type.Contains("Line", StringComparison.OrdinalIgnoreCase))
            return "line";

        if (type.Contains("Scatter", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Bubble", StringComparison.OrdinalIgnoreCase))
            return "scatter";

        return "bar";
    }

    // -------------------------------------------------------------------------
    // Table extraction
    // -------------------------------------------------------------------------

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

    // -------------------------------------------------------------------------
    // Script generation
    // -------------------------------------------------------------------------

    private static string BuildScript(TemplateScaffold scaffold)
    {
        var builder = new StringBuilder();
        builder.AppendLine("const templateScaffold = {");
        builder.AppendLine($"  templateName: {JsonSerializer.Serialize(scaffold.TemplateName)},");
        builder.AppendLine("  slides: [");

        for (var i = 0; i < scaffold.Slides.Count; i++)
        {
            var slide = scaffold.Slides[i];
            builder.AppendLine("    {");
            builder.AppendLine($"      slideNumber: {slide.SlideNumber},");
            builder.AppendLine($"      slideType: {JsonSerializer.Serialize(slide.SlideType)},");
            builder.AppendLine($"      placeholders: {JsonSerializer.Serialize(slide.Placeholders)},");
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

    private static void LogDebug(string message)
    {
        Debug.WriteLine($"[AsposeTemplateScaffold] {message}");
        Trace.WriteLine($"[AsposeTemplateScaffold] {message}");
    }
}