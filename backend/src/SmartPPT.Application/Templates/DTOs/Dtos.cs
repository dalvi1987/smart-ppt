using SmartPPT.Domain.Enums;

namespace SmartPPT.Application.Templates.Commands;

public record TemplateDto(
    Guid Id,
    string Name,
    string Description,
    string ThumbnailUrl,
    string? GeneratedScript,
    DateTime? ScriptGeneratedAt,
    TemplateCategory Category,
    int LayoutCount,
    int PlaceholderCount,
    DateTime CreatedAt,
    List<SlideLayoutDto> Layouts
);

public record SlideLayoutDto(
    Guid Id,
    string Name,
    SlideType SlideType,
    int SortOrder,
    List<PlaceholderDto> Placeholders
);

public record PlaceholderDto(
    Guid Id,
    string Name,
    string Token,
    PlaceholderType Type,
    string? MappedDataField,
    string MappingStatus
);

public record PresentationDto(
    Guid Id,
    string Title,
    Guid TemplateId,
    string TemplateName,
    string Status,
    string Source,
    int SlideCount,
    double GenerationTimeSeconds,
    string? DownloadUrl,
    DateTime CreatedAt,
    string? SlideJson
);
