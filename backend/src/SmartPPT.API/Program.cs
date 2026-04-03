using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SmartPPT.Application.AI;
using SmartPPT.Application.Presentations;
using SmartPPT.Application.Templates;
using SmartPPT.Application.Templates.Commands;
using SmartPPT.Application.Templates.Handlers;
using SmartPPT.Application.Templates.Queries;
using SmartPPT.Domain.Interfaces;
using SmartPPT.Infrastructure.AI;
using SmartPPT.Infrastructure.Data;
using SmartPPT.Infrastructure.PPTX;
using SmartPPT.Infrastructure.Repositories;
using SmartPPT.Infrastructure.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();
// ─── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<SmartPptDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npg => npg.MigrationsAssembly("SmartPPT.Infrastructure")));

// ─── Repositories & UoW ─────────────────────────────────────────────────────
builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
builder.Services.AddScoped<IPresentationRepository, PresentationRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ─── Domain Services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<ITemplateParserService, TemplateParserService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IPptxGeneratorService, PptxGeneratorService>();
builder.Services.AddScoped<IAsposePromptPptxGeneratorService, AsposePromptPptxGeneratorService>();
builder.Services.AddScoped<IAiOrchestratorService, AiOrchestratorService>();
builder.Services.AddScoped<ITemplateAwareAiService, TemplateAwareAiService>();
builder.Services.AddScoped<IPptxExtractorService, PptxExtractorService>();
builder.Services.AddScoped<IStorageService, LocalStorageService>();
builder.Services.AddScoped<IRuleEngineService, RuleEngineService>();
builder.Services.AddScoped<IPptxScriptGeneratorService, PptxScriptGeneratorService>();
builder.Services.AddScoped<ITemplateScaffoldGeneratorService, AsposeTemplateScaffoldService>();
builder.Services.AddScoped<ISemanticModelNormalizer, SemanticModelNormalizer>();

// ─── MediatR ─────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<UploadTemplateCommand>();
    cfg.RegisterServicesFromAssemblyContaining<GenerateSlideJsonCommand>();
    cfg.RegisterServicesFromAssemblyContaining<GeneratePresentationCommand>();
    cfg.RegisterServicesFromAssemblyContaining<GetAllTemplatesQuery>();
});

// ─── HTTP Clients ─────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("OpenRouter", c =>
{
    c.BaseAddress = new Uri("https://openrouter.ai/");
    c.Timeout = TimeSpan.FromSeconds(300);
});
builder.Services.AddHttpClient("AzureOpenAI", c =>
{
    c.Timeout = TimeSpan.FromSeconds(60);
});

// ─── Controllers ─────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ─── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt => opt.AddPolicy("AllowFrontend", p =>
    p.WithOrigins(
        builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? new[] { "http://localhost:5173" }
    ).AllowAnyMethod().AllowAnyHeader()));

// ─── Swagger ──────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartPPT API",
        Version = "v1",
        Description = "AI-powered PowerPoint generation platform"
    });
    c.EnableAnnotations();
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Global error handler
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    ctx.Response.ContentType = "application/json";
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var msg = ex?.Error?.Message ?? "An unexpected error occurred";
    await ctx.Response.WriteAsJsonAsync(new { error = msg });
}));

// ─── Migrate DB on startup ────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartPptDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

// ─── Middleware ───────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartPPT API v1"); c.RoutePrefix = "swagger"; });



// Serve uploaded files
var storagePath = builder.Configuration["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "smartppt");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storagePath),
    RequestPath = "/files"
});

app.UseAuthorization();
app.MapControllers();



app.Run();
