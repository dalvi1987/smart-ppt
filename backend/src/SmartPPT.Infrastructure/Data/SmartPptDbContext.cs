using Microsoft.EntityFrameworkCore;
using SmartPPT.Domain.Entities;

namespace SmartPPT.Infrastructure.Data;

public class SmartPptDbContext : DbContext
{
    public SmartPptDbContext(DbContextOptions<SmartPptDbContext> options) : base(options) { }

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<SlideLayout> SlideLayouts => Set<SlideLayout>();
    public DbSet<Placeholder> Placeholders => Set<Placeholder>();
    public DbSet<Presentation> Presentations => Set<Presentation>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Template>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Description).HasMaxLength(1000);
            e.Property(t => t.ScaffoldPath).HasMaxLength(500);
            e.Property(t => t.StoragePath).HasMaxLength(500).IsRequired();
            e.Property(t => t.ThumbnailPath).HasMaxLength(500);
            e.Property(t => t.GeneratedScript).HasColumnType("text");
            e.Property(t => t.ScriptGeneratedAt).HasColumnType("timestamp with time zone");
            e.Property(t => t.Category).HasConversion<string>();
            e.HasMany(t => t.Layouts).WithOne().HasForeignKey(l => l.TemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(t => t.IsActive);
        });

        mb.Entity<SlideLayout>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Name).HasMaxLength(100).IsRequired();
            e.Property(l => l.SlideType).HasConversion<string>();
            e.HasMany(l => l.Placeholders).WithOne().HasForeignKey(p => p.SlideLayoutId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Placeholder>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Token).HasMaxLength(100).IsRequired();
            e.Property(p => p.Type).HasConversion<string>();
            e.Property(p => p.MappingStatus).HasConversion<string>();
            e.Property(p => p.MappedDataField).HasMaxLength(200);
        });

        mb.Entity<Presentation>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).HasMaxLength(300).IsRequired();
            e.Property(p => p.Status).HasConversion<string>();
            e.Property(p => p.Source).HasConversion<string>();
            e.Property(p => p.OutputPath).HasMaxLength(500);
            e.Property(p => p.PromptUsed).HasMaxLength(5000);
            e.HasOne(p => p.Template).WithMany().HasForeignKey(p => p.TemplateId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
