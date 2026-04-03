using Microsoft.Extensions.Configuration;
using SmartPPT.Application.Templates.Commands;
using SmartPPT.Domain.Interfaces;
using SmartPPT.Infrastructure.Services;

namespace SmartPPT.Application.Templates;

public interface ITemplateService
{
    Task<TemplateDto?> RegenerateScriptAsync(Guid id);
}

public class TemplateService : ITemplateService
{
    private readonly IUnitOfWork _uow;
    private readonly ITemplateScaffoldGeneratorService _scaffoldGenerator;
    private readonly ISemanticModelNormalizer _semanticModelNormalizer;
    private readonly IConfiguration _configuration;
    private readonly IStorageService _storage;

    public TemplateService(
        IUnitOfWork uow,
        ITemplateScaffoldGeneratorService scaffoldGenerator,
        ISemanticModelNormalizer semanticModelNormalizer,
        IConfiguration configuration,
        IStorageService storage)
    {
        _uow = uow;
        _scaffoldGenerator = scaffoldGenerator;
        _semanticModelNormalizer = semanticModelNormalizer;
        _configuration = configuration;
        _storage = storage;
    }

    public async Task<TemplateDto?> RegenerateScriptAsync(Guid id)
    {
        var template = await _uow.Templates.GetByIdAsync(id);
        if (template is null)
        {
            return null;
        }

        var filePath = TemplateFilesystem.ResolveTemplateSourceFilePath(template, _configuration);

        var scaffold = await _scaffoldGenerator.GenerateAsync(filePath, template.Name);

        TemplateFilesystem.WriteScaffoldJson(template, _configuration, scaffold.Json);
        TemplateFilesystem.WriteGeneratedScript(template, _configuration, scaffold.Script);
        await NormalizeSemanticModelIfEnabledAsync(template);
        template.SetGeneratedScript(null, DateTime.UtcNow);
        await _uow.Templates.UpdateAsync(template);
        await _uow.SaveChangesAsync();

        return new TemplateDto(
            template.Id,
            template.Name,
            template.Description,
            string.IsNullOrEmpty(template.ThumbnailPath) ? string.Empty : _storage.GetPublicUrl(template.ThumbnailPath),
            TemplateFilesystem.ReadGeneratedScript(template, _configuration),
            template.ScriptGeneratedAt,
            template.Category,
            template.Layouts.Count,
            template.Layouts.Sum(l => l.Placeholders.Count),
            template.CreatedAt,
            template.Layouts.Select(l => new SlideLayoutDto(
                l.Id,
                l.Name,
                l.SlideType,
                l.SortOrder,
                l.Placeholders.Select(p => new PlaceholderDto(
                    p.Id,
                    p.Name,
                    p.Token,
                    p.Type,
                    p.MappedDataField,
                    p.MappingStatus.ToString()
                )).ToList()
            )).ToList()
        );
    }

    private async Task NormalizeSemanticModelIfEnabledAsync(Domain.Entities.Template template)
    {
        if (!_configuration.GetValue<bool>("SemanticModel:EnableNormalization"))
        {
            return;
        }

        var storageBasePath = TemplateFilesystem.GetStorageBasePath(_configuration);
        var rawScaffoldPath = TemplateFilesystem.GetScaffoldJsonFilePath(storageBasePath, template.ScaffoldPath!);
        var cleanScaffoldPath = TemplateFilesystem.GetCleanScaffoldJsonFilePath(storageBasePath, template.ScaffoldPath!);

        await _semanticModelNormalizer.NormalizeAsync(rawScaffoldPath, cleanScaffoldPath);
    }
}
