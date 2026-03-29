using SmartPPT.Domain.Enums;

namespace SmartPPT.Domain.Entities;

public class Template
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? ScaffoldPath { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string ThumbnailPath { get; private set; } = string.Empty;
    public string? GeneratedScript { get; private set; }
    public DateTime? ScriptGeneratedAt { get; private set; }
    public TemplateCategory Category { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<SlideLayout> _layouts = new();
    public IReadOnlyCollection<SlideLayout> Layouts => _layouts.AsReadOnly();

    private Template() { }

    public static Template Create(string name, string description, string storagePath, TemplateCategory category, string? scaffoldPath = null)
    {
        return new Template
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ScaffoldPath = scaffoldPath,
            StoragePath = storagePath,
            Category = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void AddLayout(SlideLayout layout) => _layouts.Add(layout);

    public void SetThumbnail(string path)
    {
        ThumbnailPath = path;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetGeneratedScript(string? generatedScript, DateTime? scriptGeneratedAt)
    {
        GeneratedScript = generatedScript;
        ScriptGeneratedAt = scriptGeneratedAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
