namespace SmartPPT.Domain.Enums;

public enum SlideType { Title, Bullet, Chart, Table, TwoColumn, SectionBreak, Image, Quote, Timeline }
public enum PlaceholderType { Text, Chart, Table, Image, Number, Date }
public enum MappingStatus { AutoMapped, ManualMapped, NeedsReview, Unmapped }
public enum PresentationStatus { Pending, Generating, Completed, Failed }
public enum GenerationSource { ManualJson,  AIPrompt, ApiData }
public enum TemplateCategory { Consulting, Sales, Finance, Product, General, Custom }
public enum ChartType { Bar, Line, Pie, Doughnut, Area, Scatter }
public enum AiProvider { OpenRouter, AzureOpenAI }
