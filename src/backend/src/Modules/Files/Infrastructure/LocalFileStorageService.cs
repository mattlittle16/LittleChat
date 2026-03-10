using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using Shared.Contracts.Interfaces;

namespace Files.Infrastructure;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;
    private static readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    private static readonly HashSet<string> _heicExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic", ".heif"
    };

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _basePath = configuration["UPLOAD_PATH"] ?? "/uploads";
        _logger = logger;

        Directory.CreateDirectory(_basePath);
    }

    public async Task<SavedFileResult> SaveAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "file";

        var ext = Path.GetExtension(safeName);
        var subDir = Guid.NewGuid().ToString("N");
        var dirPath = Path.Combine(_basePath, subDir);
        Directory.CreateDirectory(dirPath);

        string storedName;
        string contentType;

        if (_heicExtensions.Contains(ext))
        {
            // Convert HEIC/HEIF → JPEG so all browsers can render it inline
            storedName = Path.GetFileNameWithoutExtension(safeName) + ".jpg";
            contentType = "image/jpeg";

            var fullPath = Path.Combine(dirPath, storedName);
            using var image = await Image.LoadAsync(stream, ct);
            await image.SaveAsJpegAsync(fullPath, new JpegEncoder { Quality = 85 }, ct);
        }
        else
        {
            storedName = safeName;
            var fullPath = Path.Combine(dirPath, storedName);
            await using var fs = File.Create(fullPath);
            await stream.CopyToAsync(fs, ct);

            if (!_contentTypeProvider.TryGetContentType(storedName, out contentType!))
                contentType = "application/octet-stream";
        }

        var relativePath = $"{subDir}/{storedName}";
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        return new SavedFileResult(relativePath, storedName, contentType, isImage);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete uploaded file at {RelativePath}", relativePath);
        }

        return Task.CompletedTask;
    }
}
