namespace SmartPPT.Infrastructure.Models;

/// <summary>
/// Contains the extracted slide data for a PPTX template.
/// </summary>
public class SlideExtractionResult
{
    public SlideSize SlideSize { get; set; } = new();

    public List<ExtractedSlide> Slides { get; set; } = new();

    public string TemplateName { get; set; } = string.Empty;
}

public class SlideSize
{
    /// <summary>
    /// Width in inches.
    /// </summary>
    public double WidthInches { get; set; }

    /// <summary>
    /// Height in inches.
    /// </summary>
    public double HeightInches { get; set; }
}

public class ExtractedSlide
{
    public int SlideIndex { get; set; }

    /// <summary>
    /// Background color as a hex string without '#'; null means white.
    /// </summary>
    public string? BackgroundColor { get; set; }

    public List<ExtractedShape> Shapes { get; set; } = new();

    public List<ExtractedImage> Images { get; set; } = new();
}

public class ExtractedShape
{
    /// <summary>
    /// X position in inches.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y position in inches.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Width in inches.
    /// </summary>
    public double W { get; set; }

    /// <summary>
    /// Height in inches.
    /// </summary>
    public double H { get; set; }

    public string ShapeType { get; set; } = "rect";

    /// <summary>
    /// Fill color as a hex string without '#'; null means no fill / transparent.
    /// </summary>
    public string? FillColor { get; set; }

    /// <summary>
    /// Line color as a hex string without '#'.
    /// </summary>
    public string? LineColor { get; set; }

    /// <summary>
    /// Line width in inches.
    /// </summary>
    public double? LineWidth { get; set; }

    public List<ExtractedTextRun>? TextRuns { get; set; }

    public string TextAlign { get; set; } = "left";

    public string VerticalAlign { get; set; } = "top";

    /// <summary>
    /// Text margin in inches.
    /// </summary>
    public double? Margin { get; set; }
}

public class ExtractedTextRun
{
    public string Text { get; set; } = string.Empty;

    public string? FontFace { get; set; }

    /// <summary>
    /// Font size in points.
    /// </summary>
    public double FontSize { get; set; }

    /// <summary>
    /// Color as a hex string without '#'.
    /// </summary>
    public string Color { get; set; } = "000000";

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    /// <summary>
    /// True when this run should map to breakLine in the generated output.
    /// </summary>
    public bool IsLineBreak { get; set; }
}

public class ExtractedImage
{
    /// <summary>
    /// X position in inches.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y position in inches.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Width in inches.
    /// </summary>
    public double W { get; set; }

    /// <summary>
    /// Height in inches.
    /// </summary>
    public double H { get; set; }

    /// <summary>
    /// Base64 PNG data URI: "image/png;base64,..."
    /// </summary>
    public string Base64Data { get; set; } = string.Empty;
}
