namespace Shared.Contracts.Interfaces;

public interface IFileStorageService
{
    /// <summary>Saves the stream under a new GUID sub-directory. Returns the relative path (e.g., "abc123/file.pdf").</summary>
    Task<string> SaveAsync(Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>Deletes the file at the given relative path. Errors are swallowed and logged.</summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
}
