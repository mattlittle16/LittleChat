using ImageMagick;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        long actualFileSize;

        if (_heicExtensions.Contains(ext))
        {
            // Convert HEIC/HEIF → JPEG via Magick.NET so all browsers can render inline
            storedName = Path.GetFileNameWithoutExtension(safeName) + ".jpg";
            contentType = "image/jpeg";

            var fullPath = Path.Combine(dirPath, storedName);
            using var magick = new MagickImage(stream);

            // Guard against decompression bombs
            if ((ulong)magick.Width * magick.Height > 100_000_000UL)
                throw new InvalidOperationException("Image dimensions exceed the maximum allowed size (100 megapixels).");

            magick.Format = MagickFormat.Jpeg;
            magick.Quality = 85;
            await magick.WriteAsync(fullPath, ct);
            actualFileSize = new FileInfo(fullPath).Length;
        }
        else
        {
            storedName = safeName;
            var fullPath = Path.Combine(dirPath, storedName);
            await using var fs = File.Create(fullPath);
            await stream.CopyToAsync(fs, ct);
            actualFileSize = fs.Length;

            if (!_contentTypeProvider.TryGetContentType(storedName, out contentType!))
                contentType = "application/octet-stream";
        }

        var relativePath = $"{subDir}/{storedName}";
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        return new SavedFileResult(relativePath, storedName, contentType, isImage, actualFileSize);
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));
            var baseFull = Path.GetFullPath(_basePath) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(baseFull))
            {
                _logger.LogWarning("DeleteAsync rejected path traversal attempt for {RelativePath}", relativePath);
                return Task.CompletedTask;
            }
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
