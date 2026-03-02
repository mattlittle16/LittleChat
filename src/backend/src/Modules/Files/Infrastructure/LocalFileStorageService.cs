using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Interfaces;

namespace Files.Infrastructure;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _basePath = configuration["UPLOAD_PATH"] ?? "/uploads";
        _logger = logger;

        // Ensure base upload directory exists on startup
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        // Sanitize: keep only the original file name (no path traversal)
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "file";

        var subDir = Guid.NewGuid().ToString("N");
        var dirPath = Path.Combine(_basePath, subDir);
        Directory.CreateDirectory(dirPath);

        var fullPath = Path.Combine(dirPath, safeName);
        await using var fs = File.Create(fullPath);
        await stream.CopyToAsync(fs, ct);

        return $"{subDir}/{safeName}";
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
