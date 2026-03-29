using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartPPT.Application.Templates;
using SmartPPT.Application.Templates.Commands;
using SmartPPT.Application.Templates.Queries;
using SmartPPT.Domain.Enums;

namespace SmartPPT.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITemplateService _templateService;

    public TemplatesController(IMediator mediator, ITemplateService templateService)
    {
        _mediator = mediator;
        _templateService = templateService;
    }

    /// <summary>Get all active templates</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TemplateDto>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetAllTemplatesQuery(), ct));

    /// <summary>Get template by ID with full layout and placeholder info</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TemplateDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTemplateByIdQuery(id), ct);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Returns the generated JavaScript for a template.</summary>
    [HttpGet("{id:guid}/script")]
    [Produces("application/javascript")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> GetScript(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTemplateByIdQuery(id), ct);
        if (result == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(result.GeneratedScript))
        {
            return Conflict("Script has not been generated for this template.");
        }

        return Content(result.GeneratedScript, "application/javascript");
    }

    /// <summary>Upload a new .pptx template — parses layouts and auto-maps placeholders</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(TemplateDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Upload(
[FromForm] UploadTemplateRequest request,
        CancellationToken ct)
    {
        var file = request.File;
        if (file.Length == 0) return BadRequest("File is empty");
        if (!file.FileName.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .pptx files are supported");

        var result = await _mediator.Send(new UploadTemplateCommand(file, request.Name, request.Description, request.Category), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update placeholder-to-data-field mappings for a template</summary>
    [HttpPut("{id:guid}/mappings")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateMappings(Guid id,
        [FromBody] List<PlaceholderMappingInput> mappings, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdatePlaceholderMappingCommand(id, mappings), ct);
        return result ? NoContent() : NotFound();
    }

    /// <summary>Regenerates the script for an existing template.</summary>
    [HttpPost("{id:guid}/script/regenerate")]
    [ProducesResponseType(typeof(TemplateDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RegenerateScript(Guid id, CancellationToken ct)
    {
        _ = ct;
        var result = await _templateService.RegenerateScriptAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Delete (soft-delete) a template</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteTemplateCommand(id), ct);
        return NoContent();
    }

    public class UploadTemplateRequest
    {
        public IFormFile File { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TemplateCategory Category { get; set; }
    }
}
