using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPPT.Domain.Interfaces;
using System.Text.Json;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace SmartPPT.Infrastructure.PPTX;

public class PptxGeneratorService : IPptxGeneratorService
{
    private readonly IStorageService _storage;
    private readonly ILogger<PptxGeneratorService> _logger;
    private readonly string _templatesBasePath;

    public PptxGeneratorService(IStorageService storage, ILogger<PptxGeneratorService> logger,
        IConfiguration config)
    {
        _storage = storage;
        _logger = logger;
        _templatesBasePath = config["Storage:TemplatesPath"] ?? "/tmp/templates";
    }

    public async Task<string> GenerateAsync(Guid templateId, string slideJson, string outputFileName)
    {
        _logger.LogInformation("Generating PPTX {FileName} from template {TemplateId}", outputFileName, templateId);

        using var doc = System.Text.Json.JsonDocument.Parse(slideJson);
        var slidesEl = doc.RootElement.GetProperty("slides");

        // Write to temp file first (most reliable on Windows), then copy bytes, then delete
        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pptx");

        try
        {
            // ── Build the PPTX ──────────────────────────────────────────────
            using (var pptx = PresentationDocument.Create(tempPath, PresentationDocumentType.Presentation))
            {
                var presentationPart = pptx.AddPresentationPart();
                presentationPart.Presentation = new Presentation();

                // Slide size (16:9)
                presentationPart.Presentation.Append(
                    new SlideSize { Cx = 9144000, Cy = 5143500, Type = SlideSizeValues.Screen16x9 },
                    new NotesSize { Cx = 6858000, Cy = 9144000 }
                );

                // ── Slide Master (required for slides to render) ────────────
                var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
                var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();

                slideLayoutPart.SlideLayout = new SlideLayout(
                    new CommonSlideData(new ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new GroupShapeProperties(new D.TransformGroup()))),
                    new ColorMapOverride(new MasterColorMapping()));
                slideLayoutPart.SlideLayout.Save();

                slideMasterPart.SlideMaster = new SlideMaster(
                    new CommonSlideData(new ShapeTree(
                        new P.NonVisualGroupShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
                            new P.NonVisualGroupShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new GroupShapeProperties(new D.TransformGroup()))),
                    new P.ColorMap
                    {
                        Background1 = D.ColorSchemeIndexValues.Light1,
                        Text1 = D.ColorSchemeIndexValues.Dark1,
                        Background2 = D.ColorSchemeIndexValues.Light2,
                        Text2 = D.ColorSchemeIndexValues.Dark2,
                        Accent1 = D.ColorSchemeIndexValues.Accent1,
                        Accent2 = D.ColorSchemeIndexValues.Accent2,
                        Accent3 = D.ColorSchemeIndexValues.Accent3,
                        Accent4 = D.ColorSchemeIndexValues.Accent4,
                        Accent5 = D.ColorSchemeIndexValues.Accent5,
                        Accent6 = D.ColorSchemeIndexValues.Accent6,
                        Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
                        FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink
                    },
                    new SlideLayoutIdList(
                        new SlideLayoutId { Id = 2049, RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart) }));
                slideMasterPart.SlideMaster.Save();

                // Register master in presentation
                var slideMasterIdList = new SlideMasterIdList(
                    new SlideMasterId { Id = 2048, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) });
                presentationPart.Presentation.Append(slideMasterIdList);

                // ── Slides ──────────────────────────────────────────────────
                var slideIdList = new SlideIdList();
                uint slideId = 256;

                foreach (var slideEl in slidesEl.EnumerateArray())
                {
                    var slideType = slideEl.TryGetProperty("type", out var t) ? t.GetString() : "BulletSlide";
                    var data = slideEl.TryGetProperty("data", out var d) ? d : default;

                    var slidePart = presentationPart.AddNewPart<SlidePart>();
                    slidePart.AddPart(slideLayoutPart);  // link layout → slide

                    slidePart.Slide = BuildSlide(slideType!, data);
                    slidePart.Slide.Save();

                    slideIdList.Append(new SlideId
                    {
                        Id = slideId++,
                        RelationshipId = presentationPart.GetIdOfPart(slidePart)
                    });
                }

                presentationPart.Presentation.Append(slideIdList);
                presentationPart.Presentation.Save();
            }
            // ── PresentationDocument fully closed and flushed here ──────────

            // Read all bytes then upload — completely avoids any file-lock overlap
            var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
            using var uploadStream = new System.IO.MemoryStream(bytes);
            var storagePath = await _storage.UploadTemplateAsync(uploadStream, outputFileName);
            return storagePath;
        }
        finally
        {
            // Always clean up temp file
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    private static Slide BuildSlide(string slideType, JsonElement data)
    {
        var slide = new Slide();
        var csp = new CommonSlideData();
        var spTree = new ShapeTree();
        spTree.Append(new P.NonVisualGroupShapeProperties(
            new P.NonVisualDrawingProperties { Id = 1, Name = "" },
            new P.NonVisualGroupShapeDrawingProperties(),
            new P.ApplicationNonVisualDrawingProperties()));
        spTree.Append(new GroupShapeProperties(new D.TransformGroup()));

        var title = data.ValueKind != JsonValueKind.Undefined && data.TryGetProperty("title", out var t)
            ? t.GetString() ?? "" : "Slide";

        // Title shape
        spTree.Append(CreateTextShape(2, "Title", title, 457200, 274638, 8229600, 1143000, isBold: true, fontSize: 2800));

        switch (slideType)
        {
            case "TitleSlide":
                var subtitle = data.TryGetProperty("subtitle", out var sub) ? sub.GetString() ?? "" : "";
                spTree.Append(CreateTextShape(3, "Subtitle", subtitle, 457200, 1600200, 8229600, 1143000, fontSize: 1800));
                break;

            case "BulletSlide":
                var bullets = data.TryGetProperty("bullets", out var bl)
                    ? bl.EnumerateArray().Select(b => b.GetString() ?? "").ToList()
                    : new List<string>();
                spTree.Append(CreateBulletShape(3, bullets, 457200, 1600200, 8229600, 4525963));
                break;

            case "SectionBreak":
                break;

            default:
                var bodyText = data.TryGetProperty("subtitle", out var bt) ? bt.GetString() ?? "" : "";
                spTree.Append(CreateTextShape(3, "Body", bodyText, 457200, 1600200, 8229600, 4525963, fontSize: 1400));
                break;
        }

        csp.Append(spTree);
        slide.Append(csp);
        slide.Append(new ColorMapOverride(new MasterColorMapping()));
        return slide;
    }

    private static P.Shape CreateTextShape(uint id, string name, string text,
        long x, long y, long cx, long cy, bool isBold = false, int fontSize = 1800)
    {
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = name },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(new PlaceholderShape())),
            new P.ShapeProperties(new D.Transform2D(
                new D.Offset { X = x, Y = y },
                new D.Extents { Cx = cx, Cy = cy })),
            new P.TextBody(
                new D.BodyProperties(),
                new D.ListStyle(),
                new D.Paragraph(new D.Run(
                    new D.RunProperties { Language = "en-US", FontSize = fontSize, Bold = isBold },
                    new D.Text(text)))));
    }

    private static P.Shape CreateBulletShape(uint id, List<string> bullets, long x, long y, long cx, long cy)
    {
        var textBody = new P.TextBody(new D.BodyProperties(), new D.ListStyle());
        foreach (var bullet in bullets)
        {
            var para = new D.Paragraph();
            para.Append(new D.ParagraphProperties(new D.CharacterBullet { Char = "•" }));
            para.Append(new D.Run(
                new D.RunProperties { Language = "en-US", FontSize = 1600 },
                new D.Text(bullet)));
            textBody.Append(para);
        }
        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "Content" },
                new P.NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(new D.Transform2D(
                new D.Offset { X = x, Y = y },
                new D.Extents { Cx = cx, Cy = cy })),
            textBody);
    }

}