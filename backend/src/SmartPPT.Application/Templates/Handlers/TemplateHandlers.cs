using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPPT.Application.Templates;
using SmartPPT.Application.Templates.Commands;
using SmartPPT.Domain.Entities;
using SmartPPT.Domain.Enums;
using SmartPPT.Domain.Interfaces;
using SmartPPT.Infrastructure.Services;

namespace SmartPPT.Application.Templates.Handlers;

public class UploadTemplateHandler : IRequestHandler<UploadTemplateCommand, TemplateDto>
{
    private readonly IUnitOfWork _uow;
    private readonly ITemplateParserService _parser;
    private readonly IRuleEngineService _ruleEngine;
    private readonly ITemplateScaffoldGeneratorService _scaffoldGenerator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UploadTemplateHandler> _logger;

    public UploadTemplateHandler(IUnitOfWork uow, ITemplateParserService parser,
        IRuleEngineService ruleEngine,
        ITemplateScaffoldGeneratorService scaffoldGenerator,
        IConfiguration configuration, ILogger<UploadTemplateHandler> logger)
    {
        _uow = uow;
        _parser = parser;
        _ruleEngine = ruleEngine;
        _scaffoldGenerator = scaffoldGenerator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TemplateDto> Handle(UploadTemplateCommand request, CancellationToken ct)
    {
        var storageBasePath = TemplateFilesystem.GetStorageBasePath(_configuration);
        var scaffoldPath = TemplateFilesystem.CreateScaffoldPath();
        TemplateFilesystem.EnsureTemplateDirectories(storageBasePath, scaffoldPath);

        var sourceFilePath = TemplateFilesystem.GetSourceFilePath(storageBasePath, scaffoldPath, request.File.FileName);
        await using (var stream = request.File.OpenReadStream())
        {
            await using var fileStream = File.Create(sourceFilePath);
            await stream.CopyToAsync(fileStream, ct);
            await fileStream.FlushAsync(ct);
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new IOException($"Template file was not saved successfully to {sourceFilePath}");
        }

        var storagePath = Path.Combine(scaffoldPath, "source", Path.GetFileName(request.File.FileName));

        // Create template entity
        var template = Template.Create(request.Name, request.Description, storagePath, request.Category, scaffoldPath);

        // Parse pptx for layouts + placeholders
        await using var parseStream = request.File.OpenReadStream();
        var (layouts, _) = await _parser.ParseAsync(parseStream, template.Id);

        foreach (var layout in layouts)
        {
            // Auto-map placeholders via rule engine
            var mappings = _ruleEngine.AutoMapPlaceholders(layout.Placeholders);
            foreach (var mapping in mappings)
            {
                var ph = layout.Placeholders.FirstOrDefault(p => p.Id == mapping.PlaceholderId);
                if (ph != null && mapping.SuggestedField != null)
                {
                    var status = mapping.Status == "AutoMapped" ? MappingStatus.AutoMapped : MappingStatus.NeedsReview;
                    ph.MapToField(mapping.SuggestedField, status);
                }
            }
            template.AddLayout(layout);
        }

        try
        {
            _logger.LogInformation("Starting Aspose scaffold generation for template {TemplateName} from {FilePath}", template.Name, sourceFilePath);
            var scaffold = await _scaffoldGenerator.GenerateAsync(sourceFilePath, template.Name, ct);

            TemplateFilesystem.WriteScaffoldJson(template, _configuration, scaffold.Json);
            TemplateFilesystem.WriteGeneratedScript(template, _configuration, scaffold.Script);
            template.SetGeneratedScript(null, DateTime.UtcNow);
            _logger.LogInformation("Scaffold generation completed successfully for template {TemplateName}", template.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scaffold generation failed for template {TemplateName}. Upload will continue without generated scaffold.", template.Name);
        }

        await _uow.Templates.AddAsync(template, ct);
        await _uow.SaveChangesAsync(ct);

        return MapToDto(template, _configuration);
    }

    private static TemplateDto MapToDto(Template t, IConfiguration configuration) => new(
        t.Id, t.Name, t.Description, t.ThumbnailPath, TemplateFilesystem.ReadGeneratedScript(t, configuration), t.ScriptGeneratedAt, t.Category,
        t.Layouts.Count,
        t.Layouts.Sum(l => l.Placeholders.Count),
        t.CreatedAt,
        t.Layouts.Select(l => new SlideLayoutDto(
            l.Id, l.Name, l.SlideType, l.SortOrder,
            l.Placeholders.Select(p => new PlaceholderDto(
                p.Id, p.Name, p.Token, p.Type,
                p.MappedDataField, p.MappingStatus.ToString()
            )).ToList()
        )).ToList()
    );
}

public class DeleteTemplateHandler : IRequestHandler<DeleteTemplateCommand, bool>
{
    private readonly IUnitOfWork _uow;

    public DeleteTemplateHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<bool> Handle(DeleteTemplateCommand request, CancellationToken ct)
    {
        await _uow.Templates.DeleteAsync(request.Id, ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}

public class RegenerateTemplateScriptHandler : IRequestHandler<RegenerateTemplateScriptCommand, TemplateDto?>
{
    private readonly ITemplateService _templateService;

    public RegenerateTemplateScriptHandler(
        ITemplateService templateService)
    {
        _templateService = templateService;
    }

    public async Task<TemplateDto?> Handle(RegenerateTemplateScriptCommand request, CancellationToken ct)
        => await _templateService.RegenerateScriptAsync(request.TemplateId);
}

public class UpdatePlaceholderMappingHandler : IRequestHandler<UpdatePlaceholderMappingCommand, bool>
{
    private readonly IUnitOfWork _uow;

    public UpdatePlaceholderMappingHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<bool> Handle(UpdatePlaceholderMappingCommand request, CancellationToken ct)
    {
        var template = await _uow.Templates.GetByIdAsync(request.TemplateId, ct)
            ?? throw new KeyNotFoundException($"Template {request.TemplateId} not found");

        foreach (var mapping in request.Mappings)
        {
            var placeholder = template.Layouts
                .SelectMany(l => l.Placeholders)
                .FirstOrDefault(p => p.Id == mapping.PlaceholderId);

            placeholder?.MapToField(mapping.MappedDataField, MappingStatus.ManualMapped);
        }

        await _uow.Templates.UpdateAsync(template, ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}
