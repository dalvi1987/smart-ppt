using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartPPT.Application.AI;
using SmartPPT.Application.Presentations;
using SmartPPT.Application.Templates.Commands;
using SmartPPT.Domain.Enums;
using SmartPPT.Domain.Interfaces;

namespace SmartPPT.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PresentationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITemplateAwareAiService _templateAwareAiService;
    private readonly ILogger<PresentationsController> _logger;

    public PresentationsController(
        IMediator mediator,
        ITemplateAwareAiService templateAwareAiService,
        ILogger<PresentationsController> logger)
    {
        _mediator = mediator;
        _templateAwareAiService = templateAwareAiService;
        _logger = logger;
    }

    /// <summary>Get all presentations</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PresentationDto>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetAllPresentationsQuery(), ct));

    /// <summary>Get presentation by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PresentationDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPresentationQuery(id), ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Generate a PPTX from prepared slide JSON</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(PresentationDto), 201)]
    public async Task<IActionResult> Generate([FromBody] GeneratePresentationRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.SlideJson))
        {
            return BadRequest(new { error = "slideJson is required for PPT generation." });
        }

        _logger.LogInformation("Aspose PPT generation triggered (no AI) for template {TemplateId}", req.TemplateId);

        var result = await _mediator.Send(new GeneratePresentationCommand(
            req.TemplateId,
            req.Title,
            req.SlideJson,
            GenerationSource.ManualJson,
            req.PromptUsed), ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Generate structured slide JSON from a natural language prompt</summary>
    [HttpPost("generate-from-ai")]
    [ProducesResponseType(typeof(AiGenerationResult), 200)]
    public async Task<IActionResult> GenerateFromAi([FromBody] GenerateFromAiRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Template-aware AI generation triggered for template {TemplateId}", req.TemplateId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var slideJson = await _templateAwareAiService.GenerateAsync(
            req.TemplateId,
            req.Prompt,
            req.Provider ?? "OpenRouter",
            req.Model ?? "google/gemma-3n-e4b-it:free",
            req.MaxSlides,
            req.IncludeSpeakerNotes,
            req.StrictSchema,
            req.AllowedSlideTypes ?? new List<string> { "TitleSlide", "BulletSlide", "ChartSlide", "TableSlide" },
            ct);
        stopwatch.Stop();

        var slideCount = 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(slideJson);
            if (doc.RootElement.TryGetProperty("slides", out var slides))
            {
                slideCount = slides.GetArrayLength();
            }
            else if (doc.RootElement.TryGetProperty("Slides", out var legacySlides))
            {
                slideCount = legacySlides.GetArrayLength();
            }
        }
        catch
        {
            slideCount = 0;
        }

        var result = new AiGenerationResult(
            slideJson,
            slideCount,
            req.Model ?? "google/gemma-3n-e4b-it:free",
            0,
            stopwatch.Elapsed.TotalSeconds);

        return Ok(result);
    }
    /// <summary>Delete a presentation</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeletePresentationCommand(id), ct);
        return result ? NoContent() : NotFound();
    }
}

public record GeneratePresentationRequest(Guid TemplateId, string Title, string SlideJson, string? PromptUsed = null);
public record GenerateFromAiRequest(
    Guid TemplateId,
    string Prompt,
    string? Provider,
    string? Model,
    int MaxSlides = 15,
    bool IncludeSpeakerNotes = false,
    bool StrictSchema = true,
    List<string>? AllowedSlideTypes = null
);

// ─── AI Controller ────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiController : ControllerBase
{
    private readonly IMediator _mediator;
    public AiController(IMediator mediator) => _mediator = mediator;

    /// <summary>Generate structured slide JSON from a natural language prompt</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(AiGenerationResult), 200)]
    public async Task<IActionResult> Generate([FromBody] AiGenerateRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateSlideJsonCommand(
            req.Prompt,
            req.Provider ?? "OpenRouter",
            req.Model ?? "google/gemma-3n-e4b-it:free",
            req.MaxSlides,
            req.IncludeSpeakerNotes,
            req.StrictSchema,
            req.AllowedSlideTypes ?? new List<string> { "TitleSlide", "BulletSlide", "ChartSlide", "TableSlide" }
        ), ct);
        return Ok(result);
    }

    /// <summary>Get available AI providers and their models</summary>
    [HttpGet("providers")]
    public IActionResult GetProviders() => Ok(new[]
    {
        new { provider = "OpenRouter", models = new[] { "google/gemma-3n-e4b-it:free","arcee-ai/trinity-large-preview:free", "arcee-ai/trinity-mini:free", "nousresearch/hermes-3-llama-3.1-405b:free", "nvidia/nemotron-3-super-120b-a12b:free", "nvidia/llama-nemotron-embed-vl-1b-v2:free", "liquid/lfm-2.5-1.2b-thinking:free" } },
        new { provider = "AzureOpenAI", models = new[] { "gpt-4o", "gpt-4-turbo", "gpt-35-turbo" } }
    });
}

public record AiGenerateRequest(
    string Prompt,
    string? Provider,
    string? Model,
    int MaxSlides = 15,
    bool IncludeSpeakerNotes = false,
    bool StrictSchema = true,
    List<string>? AllowedSlideTypes = null
);
