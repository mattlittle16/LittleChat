namespace Shared.Contracts.Interfaces;

/// <summary>Result returned from IFileStorageService.SaveAsync after a file is stored.</summary>
public sealed record SavedFileResult(
    string RelativePath,    // e.g. "abc123/photo.jpg"
    string StoredFileName,  // may differ from input when HEIC is converted → "photo.jpg"
    string ContentType,     // e.g. "image/jpeg"
    bool   IsImage          // true for all image/* content types
);
