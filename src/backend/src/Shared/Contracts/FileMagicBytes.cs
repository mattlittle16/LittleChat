namespace Shared.Contracts;

/// <summary>
/// Validates file contents against known magic byte signatures.
/// Guards against MIME/extension spoofing — client-supplied metadata is untrustworthy.
/// </summary>
public static class FileMagicBytes
{
    /// <summary>
    /// Returns true if the header bytes match a known image format (JPEG, PNG, GIF, WebP, HEIC/HEIF).
    /// </summary>
    public static bool IsValidImage(byte[] header)
    {
        // JPEG: FF D8 FF
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return true;
        // PNG: 89 50 4E 47
        if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return true;
        // GIF87a / GIF89a: 47 49 46 38
        if (header.Length >= 4 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
            return true;
        // WebP: RIFF????WEBP (bytes 0-3 = RIFF, bytes 8-11 = WEBP)
        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;
        // HEIC/HEIF: ISO base media file — 'ftyp' box marker at byte offset 4
        if (header.Length >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
            return true;
        return false;
    }

    /// <summary>
    /// Returns true if the header bytes match a PDF signature (%PDF).
    /// </summary>
    public static bool IsValidPdf(byte[] header) =>
        header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;

    /// <summary>
    /// Returns true if the header bytes match a ZIP archive signature (PK\x03\x04).
    /// </summary>
    public static bool IsValidZip(byte[] header) =>
        header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;

    /// <summary>
    /// Validates that the uploaded file's magic bytes are consistent with the declared extension.
    /// Returns null on success, or an error message string on failure.
    /// The stream position is reset to 0 after reading.
    /// </summary>
    public static async Task<string?> ValidateAsync(Stream stream, string extension, CancellationToken ct = default)
    {
        var header = new byte[12];
        var bytesRead = await stream.ReadAsync(header, ct);
        stream.Position = 0;

        if (bytesRead < 3)
            return "File is too small to be valid.";

        var ext = extension.TrimStart('.').ToLowerInvariant();

        return ext switch
        {
            "jpg" or "jpeg" or "png" or "gif" or "webp" or "heic" or "heif"
                => IsValidImage(header) ? null : "File contents do not match the declared image type.",
            "pdf"
                => IsValidPdf(header) ? null : "File contents do not match the declared PDF type.",
            "zip"
                => IsValidZip(header) ? null : "File contents do not match the declared ZIP type.",
            // Text-based and video formats are not reliably identified by magic bytes; skip validation.
            _ => null,
        };
    }
}
