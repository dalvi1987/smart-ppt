using MediatR;
using Microsoft.Extensions.Configuration;
using SmartPPT.Application.Templates.Commands;
using SmartPPT.Domain.Interfaces;

namespace SmartPPT.Application.Templates.Queries;

public record GetAllTemplatesQuery() : IRequest<IEnumerable<TemplateDto>>;
public record GetTemplateByIdQuery(Guid Id) : IRequest<TemplateDto?>;

public class GetAllTemplatesHandler : IRequestHandler<GetAllTemplatesQuery, IEnumerable<TemplateDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IStorageService _storage;
    private readonly IConfiguration _configuration;
    public GetAllTemplatesHandler(IUnitOfWork uow, IStorageService storage, IConfiguration configuration) { _uow = uow; _storage = storage; _configuration = configuration; }

    public async Task<IEnumerable<TemplateDto>> Handle(GetAllTemplatesQuery request, CancellationToken ct)
    {
        var templates = await _uow.Templates.GetAllActiveAsync(ct);
        return templates.Select(t => new TemplateDto(
            t.Id, t.Name, t.Description,
            string.IsNullOrEmpty(t.ThumbnailPath) ? "" : _storage.GetPublicUrl(t.ThumbnailPath),
            TemplateFilesystem.ReadGeneratedScript(t, _configuration),
            t.ScriptGeneratedAt,
            t.Category, t.Layouts.Count,
            t.Layouts.Sum(l => l.Placeholders.Count),
            t.CreatedAt,
            t.Layouts.Select(l => new SlideLayoutDto(
                l.Id, l.Name, l.SlideType, l.SortOrder,
                l.Placeholders.Select(p => new PlaceholderDto(
                    p.Id, p.Name, p.Token, p.Type,
                    p.MappedDataField, p.MappingStatus.ToString()
                )).ToList()
            )).ToList()
        ));
    }
}

public class GetTemplateByIdHandler : IRequestHandler<GetTemplateByIdQuery, TemplateDto?>
{
    private readonly IUnitOfWork _uow;
    private readonly IStorageService _storage;
    private readonly IConfiguration _configuration;
    public GetTemplateByIdHandler(IUnitOfWork uow, IStorageService storage, IConfiguration configuration) { _uow = uow; _storage = storage; _configuration = configuration; }

    public async Task<TemplateDto?> Handle(GetTemplateByIdQuery request, CancellationToken ct)
    {
        var t = await _uow.Templates.GetByIdAsync(request.Id, ct);
        if (t == null) return null;
        return new TemplateDto(
            t.Id, t.Name, t.Description,
            string.IsNullOrEmpty(t.ThumbnailPath) ? "" : _storage.GetPublicUrl(t.ThumbnailPath),
            TemplateFilesystem.ReadGeneratedScript(t, _configuration),
            t.ScriptGeneratedAt,
            t.Category, t.Layouts.Count,
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
}
