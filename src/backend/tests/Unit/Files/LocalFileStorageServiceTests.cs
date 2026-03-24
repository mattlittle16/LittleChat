using Files.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Files;

/// <summary>
/// Unit tests for LocalFileStorageService.
/// These tests use a real temp directory so path-traversal logic can be exercised against
/// the actual filesystem API without mocking Path/File internals.
/// </summary>
public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IConfiguration _config;
    private readonly ILogger<LocalFileStorageService> _logger = Substitute.For<ILogger<LocalFileStorageService>>();

    public LocalFileStorageServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "lc_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["UPLOAD_PATH"] = _tempDir })
            .Build();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private LocalFileStorageService Build() => new(_config, _logger);

    // --- DeleteAsync: path-traversal guard ---

    [Fact]
    public async Task DeleteAsync_silently_ignores_path_traversal_attempt()
    {
        // An attacker-supplied path that tries to escape the upload directory
        await Build().DeleteAsync("../etc/passwd");

        // The traversal attempt is logged as a warning and the operation exits cleanly
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DeleteAsync_does_not_throw_when_file_does_not_exist()
    {
        // Deleting a non-existent file should be a no-op
        await Build().DeleteAsync("nonexistent-dir/nonexistent-file.txt");
        // No exception → test passes
    }

    [Fact]
    public async Task DeleteAsync_removes_existing_file()
    {
        // Arrange: write a real file inside the upload dir
        var subDir = Path.Combine(_tempDir, "abc123");
        Directory.CreateDirectory(subDir);
        var fullPath = Path.Combine(subDir, "test.txt");
        await File.WriteAllTextAsync(fullPath, "hello");

        // Act
        await Build().DeleteAsync("abc123/test.txt");

        // Assert
        Assert.False(File.Exists(fullPath));
    }

    // --- SaveAsync: filename sanitisation ---

    [Fact]
    public async Task SaveAsync_strips_directory_components_from_filename()
    {
        // A crafted filename containing a directory separator should be sanitised to just the file name
        using var data = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });

        // Path.GetFileName("../secrets/evil.jpg") == "evil.jpg"
        var result = await Build().SaveAsync(data, "../secrets/output.jpg");

        // The stored file should have no path traversal component
        Assert.DoesNotContain("..", result.RelativePath);
        Assert.DoesNotContain("secrets", result.RelativePath);
    }

    [Fact]
    public async Task SaveAsync_falls_back_to_generic_name_for_blank_filename()
    {
        using var data = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

        var result = await Build().SaveAsync(data, "   ");

        Assert.False(string.IsNullOrWhiteSpace(result.RelativePath));
    }
}
