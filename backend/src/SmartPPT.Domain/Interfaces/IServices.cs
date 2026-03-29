using SmartPPT.Domain.Entities;

namespace SmartPPT.Domain.Interfaces;

public interface ITemplateParserService
{
    Task<(List<SlideLayout> layouts, int placeholderCount)> ParseAsync(Stream pptxStream, Guid templateId);
}


public interface IPptxGeneratorService
{
    Task<string> GenerateAsync(Guid templateId, string slideJson, string outputFileName);
}

public interface IAsposePromptPptxGeneratorService
{
    Task<AsposePromptPptxGenerationResult> GenerateAsync(Template template, string userMessage, string outputFileName, CancellationToken ct = default);
}

public interface IAiOrchestratorService
{
    Task<string> GenerateSlideJsonAsync(string prompt, AiGenerationOptions options);
}

public interface IStorageService
{
    Task<string> UploadTemplateAsync(Stream stream, string fileName);
    Task<Stream> DownloadAsync(string path);
    Task DeleteAsync(string path);
    string GetPublicUrl(string path);
}

public interface IRuleEngineService
{
    List<PlaceholderMapping> AutoMapPlaceholders(IEnumerable<Placeholder> placeholders);
}

public record AiGenerationOptions(
    string Provider,
    string Model,
    int MaxSlides,
    bool IncludeSpeakerNotes,
    bool StrictSchema,
    List<string> AllowedSlideTypes
);

public record PlaceholderMapping(
    Guid PlaceholderId,
    string Token,
    string? SuggestedField,
    string MappingRule,
    string Status
);

public record AsposePromptPptxGenerationResult(
    string OutputPath,
    string GeneratedJson,
    int SlideCount
);
