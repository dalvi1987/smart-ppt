using MediatR;
using Microsoft.AspNetCore.Http;
using SmartPPT.Domain.Enums;

namespace SmartPPT.Application.Templates.Commands;

// Upload Template
public record UploadTemplateCommand(
    IFormFile File,
    string Name,
    string Description,
    TemplateCategory Category
) : IRequest<TemplateDto>;

// Delete Template
public record DeleteTemplateCommand(Guid Id) : IRequest<bool>;

// Update Placeholder Mapping
public record UpdatePlaceholderMappingCommand(
    Guid TemplateId,
    List<PlaceholderMappingInput> Mappings
) : IRequest<bool>;

public record RegenerateTemplateScriptCommand(Guid TemplateId) : IRequest<TemplateDto?>;

public record PlaceholderMappingInput(Guid PlaceholderId, string MappedDataField, string MappingRule);
