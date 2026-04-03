using Microsoft.Extensions.Configuration;
using SmartPPT.Domain.Entities;

namespace SmartPPT.Application.Templates;

internal static class TemplateFilesystem
{
    private const string ScaffoldFileName = "template.scaffold.js";
    private const string ScaffoldJsonFileName = "template.scaffold.json";
    private const string CleanScaffoldJsonFileName = "clean_template.scaffold.json";

    public static string GetStorageBasePath(IConfiguration configuration) =>
        configuration["Storage:BasePath"] ?? Path.Combine(Path.GetTempPath(), "smartppt");

    public static string CreateScaffoldPath() =>
        Path.Combine("templates", Guid.NewGuid().ToString("N"));

    public static string GetTemplateRoot(string storageBasePath, string scaffoldPath) =>
        EnsureUnderBasePath(storageBasePath, Path.GetFullPath(Path.Combine(storageBasePath, NormalizeRelative(scaffoldPath))));

    public static string GetSourceDirectory(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetTemplateRoot(storageBasePath, scaffoldPath), "source");

    public static string GetArtifactsDirectory(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetTemplateRoot(storageBasePath, scaffoldPath), "artifacts");

    public static string GetSourceFilePath(string storageBasePath, string scaffoldPath, string originalFileName) =>
        Path.Combine(GetSourceDirectory(storageBasePath, scaffoldPath), Path.GetFileName(originalFileName));

    public static string GetScaffoldFilePath(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetArtifactsDirectory(storageBasePath, scaffoldPath), ScaffoldFileName);

    public static string GetScaffoldJsonFilePath(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetArtifactsDirectory(storageBasePath, scaffoldPath), ScaffoldJsonFileName);

    public static string GetCleanScaffoldJsonFilePath(string storageBasePath, string scaffoldPath) =>
        Path.Combine(GetArtifactsDirectory(storageBasePath, scaffoldPath), CleanScaffoldJsonFileName);

    public static void EnsureTemplateDirectories(string storageBasePath, string scaffoldPath)
    {
        Directory.CreateDirectory(GetSourceDirectory(storageBasePath, scaffoldPath));
        Directory.CreateDirectory(GetArtifactsDirectory(storageBasePath, scaffoldPath));
    }

    public static string ResolveTemplateSourceFilePath(Template template, IConfiguration configuration)
    {
        var storageBasePath = GetStorageBasePath(configuration);

        if (!string.IsNullOrWhiteSpace(template.ScaffoldPath))
        {
            var sourceDirectory = GetSourceDirectory(storageBasePath, template.ScaffoldPath);
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException($"Template source directory not found at {sourceDirectory}");
            }

            var sourceFiles = Directory.GetFiles(sourceDirectory, "*.pptx", SearchOption.TopDirectoryOnly);
            if (sourceFiles.Length == 0)
            {
                throw new FileNotFoundException($"No template source file found under {sourceDirectory}", sourceDirectory);
            }

            return sourceFiles[0];
        }

        return Path.Combine(storageBasePath, NormalizeRelative(template.StoragePath));
    }

    public static string? ReadGeneratedScript(Template template, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(template.ScaffoldPath))
        {
            return template.GeneratedScript;
        }

        var scaffoldFilePath = GetScaffoldFilePath(GetStorageBasePath(configuration), template.ScaffoldPath);
        return File.Exists(scaffoldFilePath) ? File.ReadAllText(scaffoldFilePath) : null;
    }

    public static void WriteGeneratedScript(Template template, IConfiguration configuration, string script)
    {
        if (string.IsNullOrWhiteSpace(template.ScaffoldPath))
        {
            throw new InvalidOperationException("ScaffoldPath is required to store the generated scaffold on disk.");
        }

        var storageBasePath = GetStorageBasePath(configuration);
        EnsureTemplateDirectories(storageBasePath, template.ScaffoldPath);
        File.WriteAllText(GetScaffoldFilePath(storageBasePath, template.ScaffoldPath), script);
    }

    public static void WriteScaffoldJson(Template template, IConfiguration configuration, string json)
    {
        if (string.IsNullOrWhiteSpace(template.ScaffoldPath))
        {
            throw new InvalidOperationException("ScaffoldPath is required to store the scaffold JSON on disk.");
        }

        var storageBasePath = GetStorageBasePath(configuration);
        EnsureTemplateDirectories(storageBasePath, template.ScaffoldPath);
        File.WriteAllText(GetScaffoldJsonFilePath(storageBasePath, template.ScaffoldPath), json);
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

    private static string EnsureUnderBasePath(string storageBasePath, string fullPath)
    {
        var baseFullPath = Path.GetFullPath(storageBasePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Resolved path '{fullPath}' is outside storage base path '{baseFullPath}'.");
        }

        return fullPath;
    }
}
