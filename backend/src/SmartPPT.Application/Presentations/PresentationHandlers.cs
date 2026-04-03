using MediatR;
using Microsoft.Extensions.Logging;
using SmartPPT.Application.Templates.Commands;
using SmartPPT.Domain.Entities;
using SmartPPT.Domain.Enums;
using SmartPPT.Domain.Interfaces;
using System.Diagnostics;

namespace SmartPPT.Application.Presentations;

// Commands
public record GeneratePresentationCommand(
    Guid TemplateId,
    string Title,
    string SlideJson,
    GenerationSource Source,
    string? PromptUsed = null
) : IRequest<PresentationDto>;

public record GetPresentationQuery(Guid Id) : IRequest<PresentationDto?>;
public record GetAllPresentationsQuery() : IRequest<IEnumerable<PresentationDto>>;
public record DeletePresentationCommand(Guid Id) : IRequest<bool>;

// Delete Handler
public class DeletePresentationHandler : IRequestHandler<DeletePresentationCommand, bool>
{
    private readonly IUnitOfWork _uow;
    private readonly IStorageService _storage;

    public DeletePresentationHandler(IUnitOfWork uow, IStorageService storage)
    {
        _uow = uow; _storage = storage;
    }

    public async Task<bool> Handle(DeletePresentationCommand request, CancellationToken ct)
    {
        var presentation = await _uow.Presentations.GetByIdAsync(request.Id, ct);
        if (presentation == null) return false;

        if (!string.IsNullOrEmpty(presentation.OutputPath))
            try { await _storage.DeleteAsync(presentation.OutputPath); } catch { }

        await _uow.Presentations.DeleteAsync(request.Id, ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}

// Handler
public class GeneratePresentationHandler : IRequestHandler<GeneratePresentationCommand, PresentationDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IPptxGeneratorService _generator;
    private readonly IAsposePromptPptxGeneratorService _asposeGenerator;
    private readonly IStorageService _storage;
    private readonly ILogger<GeneratePresentationHandler> _logger;

    public GeneratePresentationHandler(
        IUnitOfWork uow,
        IPptxGeneratorService generator,
        IAsposePromptPptxGeneratorService asposeGenerator,
        IStorageService storage,
        ILogger<GeneratePresentationHandler> logger)
    {
        _uow = uow;
        _generator = generator;
        _asposeGenerator = asposeGenerator;
        _storage = storage;
        _logger = logger;
    }

    public async Task<PresentationDto> Handle(GeneratePresentationCommand request, CancellationToken ct)
    {
        var template = await _uow.Templates.GetByIdAsync(request.TemplateId, ct)
            ?? throw new KeyNotFoundException($"Template {request.TemplateId} not found");

        var presentation = Presentation.Create(request.TemplateId, request.Title, request.Source, request.PromptUsed);

        var slideCount = CountSlides(request.SlideJson);
        presentation.SetSlideJson(request.SlideJson, slideCount);
        presentation.MarkGenerating();

        await _uow.Presentations.AddAsync(presentation, ct);
        await _uow.SaveChangesAsync(ct);

        var sw = Stopwatch.StartNew();
        try
        {
            var outputFileName = $"{presentation.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.pptx";
            string outputPath;

            if (request.Source == GenerationSource.ManualJson &&
                !string.IsNullOrWhiteSpace(request.SlideJson) &&
                !string.IsNullOrWhiteSpace(template.ScaffoldPath))
            {
                _logger.LogInformation(
                    "Aspose PPT generation triggered (no AI) for presentation {PresentationId} and template {TemplateId}",
                    presentation.Id,
                    request.TemplateId);

                var generated = await _asposeGenerator.GenerateAsync(template, request.SlideJson, outputFileName, ct);
                presentation.SetSlideJson(generated.GeneratedJson, generated.SlideCount);
                outputPath = generated.OutputPath;
            }
            else
            {
                _logger.LogInformation(
                    "Using legacy PPT generator for presentation {PresentationId} and template {TemplateId}",
                    presentation.Id,
                    request.TemplateId);

                outputPath = await _generator.GenerateAsync(request.TemplateId, request.SlideJson, outputFileName);
            }

            sw.Stop();

            presentation.MarkCompleted(outputPath, sw.Elapsed.TotalSeconds);
        }
        catch
        {
            presentation.MarkFailed();
            throw;
        }
        finally
        {
            await _uow.Presentations.UpdateAsync(presentation, ct);
            await _uow.SaveChangesAsync(ct);
        }

        return MapToDto(presentation, template.Name, _storage);
    }

    private static int CountSlides(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("slides", out var slides))
                return slides.GetArrayLength();
        }
        catch { }
        return 0;
    }

    internal static PresentationDto MapToDto(Presentation p, string templateName, IStorageService storage) => new(
        p.Id, p.Title, p.TemplateId, templateName,
        p.Status.ToString(), p.Source.ToString(),
        p.SlideCount, p.GenerationTimeSeconds,
        p.OutputPath != null ? storage.GetPublicUrl(p.OutputPath) : null,
        p.CreatedAt,
        p.SlideJson
    );
}

public class GetAllPresentationsHandler : IRequestHandler<GetAllPresentationsQuery, IEnumerable<PresentationDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IStorageService _storage;

    public GetAllPresentationsHandler(IUnitOfWork uow, IStorageService storage) { _uow = uow; _storage = storage; }

    public async Task<IEnumerable<PresentationDto>> Handle(GetAllPresentationsQuery request, CancellationToken ct)
    {
        var presentations = await _uow.Presentations.GetAllAsync(ct);
        return presentations.Select(p => GeneratePresentationHandler.MapToDto(p, p.Template?.Name ?? "Unknown", _storage));
    }
}

public class GetPresentationHandler : IRequestHandler<GetPresentationQuery, PresentationDto?>
{
    private readonly IUnitOfWork _uow;
    private readonly IStorageService _storage;

    public GetPresentationHandler(IUnitOfWork uow, IStorageService storage) { _uow = uow; _storage = storage; }

    public async Task<PresentationDto?> Handle(GetPresentationQuery request, CancellationToken ct)
    {
        var p = await _uow.Presentations.GetByIdAsync(request.Id, ct);
        if (p == null) return null;
        return GeneratePresentationHandler.MapToDto(p, p.Template?.Name ?? "Unknown", _storage);
    }
}
