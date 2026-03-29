using MediatR;
using SmartPPT.Domain.Interfaces;

namespace SmartPPT.Application.AI;

public record GenerateSlideJsonCommand(
    string Prompt,
    string Provider,       // "OpenRouter" | "AzureOpenAI"
    string Model,
    int MaxSlides,
    bool IncludeSpeakerNotes,
    bool StrictSchema,
    List<string> AllowedSlideTypes
) : IRequest<AiGenerationResult>;

public record AiGenerationResult(
    string SlideJson,
    int SlideCount,
    string ModelUsed,
    int TokensUsed,
    double GenerationSeconds
);

public class GenerateSlideJsonHandler : IRequestHandler<GenerateSlideJsonCommand, AiGenerationResult>
{
    private readonly IAiOrchestratorService _ai;

    public GenerateSlideJsonHandler(IAiOrchestratorService ai) => _ai = ai;

    public async Task<AiGenerationResult> Handle(GenerateSlideJsonCommand request, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var options = new AiGenerationOptions(
            request.Provider,
            request.Model,
            request.MaxSlides,
            request.IncludeSpeakerNotes,
            request.StrictSchema,
            request.AllowedSlideTypes
        );

        var json = await _ai.GenerateSlideJsonAsync(request.Prompt, options);
        sw.Stop();

        // Count slides
        int slideCount = 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("slides", out var slides))
                slideCount = slides.GetArrayLength();
        }
        catch { }

        return new AiGenerationResult(json, slideCount, request.Model, 0, sw.Elapsed.TotalSeconds);
    }
}
