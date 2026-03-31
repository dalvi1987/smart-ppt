using System.Text.Json.Serialization;

namespace SmartPPT.Infrastructure.Models;

public class TemplateScaffold
{
    public string TemplateName { get; set; } = string.Empty;
    public List<TemplateScaffoldSlide> Slides { get; set; } = new();
}

public class TemplateScaffoldSlide
{
    public int SlideNumber { get; set; }
    public string SlideType { get; set; } = "Custom";
    public List<TemplateScaffoldPlaceholder> Placeholders { get; set; } = new();
    public List<TemplateScaffoldChart> Charts { get; set; } = new();
    public List<TemplateScaffoldTable> Tables { get; set; } = new();

    [JsonIgnore]
    public string? TitleText { get; set; }

    [JsonIgnore]
    public List<string> BulletPlaceholders { get; set; } = new();
}

public class TemplateScaffoldPlaceholder
{
    public string Key { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string Type { get; set; } = "Text";
}

public class TemplateScaffoldChart
{
    public int ChartIndex { get; set; }
    public string ChartType { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<TemplateScaffoldSeries> Series { get; set; } = new();
}

public class TemplateScaffoldSeries
{
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("Data")]
    public List<string> Values { get; set; } = new();
}

public class TemplateScaffoldTable
{
    public int RowCount { get; set; }
    public int ColumnCount { get; set; }
    public List<List<string>> Rows { get; set; } = new();
}

public record TemplateScaffoldArtifacts(string Json, string Script);
