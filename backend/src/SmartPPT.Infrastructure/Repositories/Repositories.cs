using Microsoft.EntityFrameworkCore;
using SmartPPT.Domain.Entities;
using SmartPPT.Domain.Interfaces;
using SmartPPT.Infrastructure.Data;

namespace SmartPPT.Infrastructure.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly SmartPptDbContext _db;
    public TemplateRepository(SmartPptDbContext db) => _db = db;

    public async Task<Template?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Templates
            .Include(t => t.Layouts).ThenInclude(l => l.Placeholders)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IEnumerable<Template>> GetAllActiveAsync(CancellationToken ct = default) =>
        await _db.Templates
            .Include(t => t.Layouts).ThenInclude(l => l.Placeholders)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<Template> AddAsync(Template template, CancellationToken ct = default)
    {
        _db.Templates.Add(template);
        return template;
    }

    public async Task UpdateAsync(Template template, CancellationToken ct = default) =>
        _db.Templates.Update(template);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await GetByIdAsync(id, ct);
        if (template != null) { template.Deactivate(); _db.Templates.Update(template); }
    }
}

public class PresentationRepository : IPresentationRepository
{
    private readonly SmartPptDbContext _db;
    public PresentationRepository(SmartPptDbContext db) => _db = db;

    public async Task<Presentation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Presentations.Include(p => p.Template).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Presentation>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Presentations.Include(p => p.Template)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task<Presentation> AddAsync(Presentation p, CancellationToken ct = default)
    {
        _db.Presentations.Add(p);
        return p;
    }

    public async Task UpdateAsync(Presentation p, CancellationToken ct = default) =>
        _db.Presentations.Update(p);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.Presentations.FindAsync(new object[] { id }, ct);
        if (p != null) _db.Presentations.Remove(p);
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly SmartPptDbContext _db;

    public UnitOfWork(SmartPptDbContext db, ITemplateRepository templates, IPresentationRepository presentations)
    {
        _db = db;
        Templates = templates;
        Presentations = presentations;
    }

    public ITemplateRepository Templates { get; }
    public IPresentationRepository Presentations { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}