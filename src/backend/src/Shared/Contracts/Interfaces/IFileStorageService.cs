namespace Shared.Contracts.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Saves the stream under a new GUID sub-directory.
    /// HEIC/HEIF files are automatically converted to JPEG.
    /// Returns a SavedFileResult with the relative path and metadata.
    /// </summary>
    Task<SavedFileResult> SaveAsync(Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>Deletes the file at the given relative path. Errors are swallowed and logged.</summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
}
