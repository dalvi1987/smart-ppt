using SmartPPT.Domain.Enums;

namespace SmartPPT.Domain.Entities;

public class Presentation
{
    public Guid Id { get; private set; }
    public Guid TemplateId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public PresentationStatus Status { get; private set; }
    public GenerationSource Source { get; private set; }
    public string? OutputPath { get; private set; }
    public string? SlideJson { get; private set; }
    public string? PromptUsed { get; private set; }
    public int SlideCount { get; private set; }
    public double GenerationTimeSeconds { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Template? Template { get; private set; }

    private Presentation() { }

    public static Presentation Create(Guid templateId, string title, GenerationSource source, string? prompt = null)
    {
        return new Presentation
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            Title = title,
            Status = PresentationStatus.Pending,
            Source = source,
            PromptUsed = prompt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void SetSlideJson(string json, int slideCount)
    {
        SlideJson = json;
        SlideCount = slideCount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkGenerating() { Status = PresentationStatus.Generating; UpdatedAt = DateTime.UtcNow; }

    public void MarkCompleted(string outputPath, double generationTime)
    {
        OutputPath = outputPath;
        Status = PresentationStatus.Completed;
        GenerationTimeSeconds = generationTime;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed() { Status = PresentationStatus.Failed; UpdatedAt = DateTime.UtcNow; }
}
