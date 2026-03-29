using SmartPPT.Domain.Enums;

namespace SmartPPT.Domain.Entities;

public class SlideLayout
{
    public Guid Id { get; private set; }
    public Guid TemplateId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SlideType SlideType { get; private set; }
    public int SortOrder { get; private set; }

    private readonly List<Placeholder> _placeholders = new();
    public IReadOnlyCollection<Placeholder> Placeholders => _placeholders.AsReadOnly();

    private SlideLayout() { }

    public static SlideLayout Create(Guid templateId, string name, SlideType slideType, int sortOrder)
    {
        return new SlideLayout
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            Name = name,
            SlideType = slideType,
            SortOrder = sortOrder
        };
    }

    public void AddPlaceholder(Placeholder placeholder) => _placeholders.Add(placeholder);
}

public class Placeholder
{
    public Guid Id { get; private set; }
    public Guid SlideLayoutId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public PlaceholderType Type { get; private set; }
    public string? MappedDataField { get; private set; }
    public MappingStatus MappingStatus { get; private set; }

    private Placeholder() { }

    public static Placeholder Create(Guid layoutId, string name, string token, PlaceholderType type)
    {
        return new Placeholder
        {
            Id = Guid.NewGuid(),
            SlideLayoutId = layoutId,
            Name = name,
            Token = token,
            Type = type,
            MappingStatus = MappingStatus.Unmapped
        };
    }

    public void MapToField(string dataField, MappingStatus status)
    {
        MappedDataField = dataField;
        MappingStatus = status;
    }
}
