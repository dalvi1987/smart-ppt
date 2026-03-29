using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using SmartPPT.Infrastructure.Models;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace SmartPPT.Infrastructure.Services;

public interface IPptxExtractorService
{
    SlideExtractionResult Extract(string pptxFilePath, string templateName);
}

public class PptxExtractorService : IPptxExtractorService
{
    private const double EmuPerInch = 914400.0;

    public SlideExtractionResult Extract(string pptxFilePath, string templateName)
    {
        var result = new SlideExtractionResult
        {
            TemplateName = templateName
        };

        using var presentationDocument = PresentationDocument.Open(pptxFilePath, false);

        var presentationPart = presentationDocument.PresentationPart
            ?? throw new InvalidOperationException("No presentation part found.");

        var slideSize = presentationPart.Presentation.SlideSize;
        if (slideSize is not null)
        {
            result.SlideSize.WidthInches = Math.Round((slideSize.Cx?.Value ?? 0) / EmuPerInch, 2);
            result.SlideSize.HeightInches = Math.Round((slideSize.Cy?.Value ?? 0) / EmuPerInch, 2);
        }

        var slideIds = presentationPart.Presentation.SlideIdList?.Elements<SlideId>().ToList()
            ?? new List<SlideId>();

        for (var index = 0; index < slideIds.Count; index++)
        {
            var relationshipId = slideIds[index].RelationshipId?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                continue;
            }

            var slidePart = (SlidePart)presentationPart.GetPartById(relationshipId);
            result.Slides.Add(ExtractSlide(slidePart, index));
        }

        return result;
    }

    private static ExtractedSlide ExtractSlide(SlidePart slidePart, int slideIndex)
    {
        var slide = new ExtractedSlide
        {
            SlideIndex = slideIndex
        };

        var shapeTree = slidePart.Slide.CommonSlideData?.ShapeTree;
        if (shapeTree is null)
        {
            return slide;
        }

        foreach (var shape in shapeTree.Elements<P.Shape>())
        {
            var extractedShape = ExtractShape(shape, slidePart);
            if (extractedShape is not null)
            {
                slide.Shapes.Add(extractedShape);
            }
        }

        foreach (var picture in shapeTree.Elements<P.Picture>())
        {
            var extractedImage = ExtractImage(picture, slidePart);
            if (extractedImage is not null)
            {
                slide.Images.Add(extractedImage);
            }
        }

        return slide;
    }

    private static ExtractedShape? ExtractShape(P.Shape shape, SlidePart slidePart)
    {
        var transform = shape.ShapeProperties?.Transform2D;
        if (transform is null)
        {
            return null;
        }

        var extractedShape = new ExtractedShape
        {
            X = Math.Round((transform.Offset?.X?.Value ?? 0) / EmuPerInch, 2),
            Y = Math.Round((transform.Offset?.Y?.Value ?? 0) / EmuPerInch, 2),
            W = Math.Round((transform.Extents?.Cx?.Value ?? 0) / EmuPerInch, 2),
            H = Math.Round((transform.Extents?.Cy?.Value ?? 0) / EmuPerInch, 2)
        };

        var textBody = shape.TextBody;
        if (textBody is not null)
        {
            extractedShape.ShapeType = "text";
            extractedShape.TextRuns = ExtractTextRuns(textBody, slidePart);

            var firstParagraph = textBody.Descendants<A.Paragraph>().FirstOrDefault();
            var alignment = firstParagraph?.ParagraphProperties?.Alignment?.Value;
            if (alignment is not null && alignment == A.TextAlignmentTypeValues.Center)
            {
                extractedShape.TextAlign = "center";
            }
            else if (alignment is not null && alignment == A.TextAlignmentTypeValues.Right)
            {
                extractedShape.TextAlign = "right";
            }
            else
            {
                extractedShape.TextAlign = "left";
            }

            var bodyProperties = textBody.GetFirstChild<A.BodyProperties>();
            var verticalAlignment = bodyProperties?.Anchor?.Value;
            if (verticalAlignment is not null && verticalAlignment == A.TextAnchoringTypeValues.Center)
            {
                extractedShape.VerticalAlign = "middle";
            }
            else if (verticalAlignment is not null && verticalAlignment == A.TextAnchoringTypeValues.Bottom)
            {
                extractedShape.VerticalAlign = "bottom";
            }
            else
            {
                extractedShape.VerticalAlign = "top";
            }

            if (bodyProperties?.LeftInset?.Value is not null)
            {
                extractedShape.Margin = Math.Round(bodyProperties.LeftInset.Value / EmuPerInch, 2);
            }
        }

        return extractedShape;
    }

    private static List<ExtractedTextRun> ExtractTextRuns(P.TextBody textBody, SlidePart slidePart)
    {
        _ = slidePart;

        var extractedRuns = new List<ExtractedTextRun>();
        var paragraphs = textBody.Descendants<A.Paragraph>().ToList();

        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Count; paragraphIndex++)
        {
            var paragraph = paragraphs[paragraphIndex];
            var paragraphStartIndex = extractedRuns.Count;

            foreach (var run in paragraph.Elements<A.Run>())
            {
                var runProperties = run.RunProperties;
                var solidFill = runProperties?.GetFirstChild<A.SolidFill>();

                extractedRuns.Add(new ExtractedTextRun
                {
                    Text = run.Text?.Text ?? string.Empty,
                    FontFace = runProperties?.GetFirstChild<A.LatinFont>()?.Typeface?.Value,
                    FontSize = runProperties?.FontSize?.Value is int fontSize
                        ? Math.Round(fontSize / 100.0, 2)
                        : 12.0,
                    Color = ResolveColor(solidFill, slidePart) ?? "000000",
                    Bold = runProperties?.Bold?.Value ?? false,
                    Italic = runProperties?.Italic?.Value ?? false,
                    IsLineBreak = false
                });
            }

            if (paragraphStartIndex < extractedRuns.Count && paragraphIndex < paragraphs.Count - 1)
            {
                extractedRuns[^1].IsLineBreak = true;
            }
        }

        return extractedRuns;
    }

    private static ExtractedImage? ExtractImage(P.Picture picture, SlidePart slidePart)
    {
        var transform = picture.ShapeProperties?.Transform2D;
        if (transform is null)
        {
            return null;
        }

        var blip = picture.Descendants<A.Blip>().FirstOrDefault();
        var relationshipId = blip?.Embed?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return null;
        }

        var extractedImage = new ExtractedImage
        {
            X = Math.Round((transform.Offset?.X?.Value ?? 0) / EmuPerInch, 2),
            Y = Math.Round((transform.Offset?.Y?.Value ?? 0) / EmuPerInch, 2),
            W = Math.Round((transform.Extents?.Cx?.Value ?? 0) / EmuPerInch, 2),
            H = Math.Round((transform.Extents?.Cy?.Value ?? 0) / EmuPerInch, 2)
        };

        try
        {
            var imagePart = (ImagePart)slidePart.GetPartById(relationshipId);
            using var stream = imagePart.GetStream();
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);

            var base64String = Convert.ToBase64String(memoryStream.ToArray());
            extractedImage.Base64Data = $"{imagePart.ContentType};base64,{base64String}";
        }
        catch
        {
            return null;
        }

        return extractedImage;
    }

    private static string? ResolveColor(A.SolidFill? solidFill, SlidePart slidePart)
    {
        if (solidFill is null)
        {
            return null;
        }

        var rgbColor = solidFill.RgbColorModelHex?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(rgbColor))
        {
            return rgbColor.TrimStart('#').ToUpperInvariant();
        }

        var schemeColor = solidFill.SchemeColor?.Val;
        if (schemeColor is not null && schemeColor.HasValue)
        {
            return ResolveThemeColor(schemeColor.Value, slidePart);
        }

        var systemColor = solidFill.SystemColor?.LastColor?.Value;
        if (!string.IsNullOrWhiteSpace(systemColor))
        {
            return systemColor.TrimStart('#').ToUpperInvariant();
        }

        return null;
    }

    private static string? ResolveThemeColor(A.SchemeColorValues schemeColorValue, SlidePart slidePart)
    {
        var themePart = slidePart.SlideLayoutPart?.SlideMasterPart?.ThemePart;
        var colorScheme = themePart?.Theme?.ThemeElements?.ColorScheme;
        if (colorScheme is null)
        {
            return null;
        }

        A.Color2Type? colorElement = null;

        if (schemeColorValue == A.SchemeColorValues.Dark1)
        {
            colorElement = colorScheme.Dark1Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Light1)
        {
            colorElement = colorScheme.Light1Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Dark2)
        {
            colorElement = colorScheme.Dark2Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Light2)
        {
            colorElement = colorScheme.Light2Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Accent1)
        {
            colorElement = colorScheme.Accent1Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Accent2)
        {
            colorElement = colorScheme.Accent2Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Accent3)
        {
            colorElement = colorScheme.Accent3Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Accent4)
        {
            colorElement = colorScheme.Accent4Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Accent5)
        {
            colorElement = colorScheme.Accent5Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Accent6)
        {
            colorElement = colorScheme.Accent6Color;
        }
        else if (schemeColorValue == A.SchemeColorValues.Hyperlink)
        {
            colorElement = colorScheme.Hyperlink;
        }
        else if (schemeColorValue == A.SchemeColorValues.FollowedHyperlink)
        {
            colorElement = colorScheme.FollowedHyperlinkColor;
        }

        var rgbColor = colorElement?.RgbColorModelHex?.Val?.Value;
        if (!string.IsNullOrWhiteSpace(rgbColor))
        {
            return rgbColor.TrimStart('#').ToUpperInvariant();
        }

        var systemColor = colorElement?.SystemColor?.LastColor?.Value;
        if (!string.IsNullOrWhiteSpace(systemColor))
        {
            return systemColor.TrimStart('#').ToUpperInvariant();
        }

        return null;
    }
}
