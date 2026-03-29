using SmartPPT.Domain.Entities;

namespace SmartPPT.Domain.Interfaces;

public interface ITemplateRepository
{
    Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Template>> GetAllActiveAsync(CancellationToken ct = default);
    Task<Template> AddAsync(Template template, CancellationToken ct = default);
    Task UpdateAsync(Template template, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IPresentationRepository
{
    Task<Presentation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Presentation>> GetAllAsync(CancellationToken ct = default);
    Task<Presentation> AddAsync(Presentation presentation, CancellationToken ct = default);
    Task UpdateAsync(Presentation presentation, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    ITemplateRepository Templates { get; }
    IPresentationRepository Presentations { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}