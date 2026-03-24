using Shared.Contracts;

namespace Tests.Unit.Files;

public class FileMagicBytesTests
{
    // --- IsValidImage ---

    [Fact]
    public void IsValidImage_returns_true_for_jpeg()
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00];
        Assert.True(FileMagicBytes.IsValidImage(header));
    }

    [Fact]
    public void IsValidImage_returns_true_for_png()
    {
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x00, 0x00];
        Assert.True(FileMagicBytes.IsValidImage(header));
    }

    [Fact]
    public void IsValidImage_returns_true_for_gif()
    {
        byte[] header = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61];
        Assert.True(FileMagicBytes.IsValidImage(header));
    }

    [Fact]
    public void IsValidImage_returns_true_for_webp()
    {
        // RIFF????WEBP
        byte[] header =
        [
            0x52, 0x49, 0x46, 0x46,  // RIFF
            0x00, 0x00, 0x00, 0x00,  // size (don't care)
            0x57, 0x45, 0x42, 0x50,  // WEBP
        ];
        Assert.True(FileMagicBytes.IsValidImage(header));
    }

    [Fact]
    public void IsValidImage_returns_true_for_heic()
    {
        // ISO base media — 'ftyp' at byte offset 4
        byte[] header =
        [
            0x00, 0x00, 0x00, 0x20,  // box size
            0x66, 0x74, 0x79, 0x70,  // 'ftyp'
            0x00, 0x00, 0x00, 0x00,
        ];
        Assert.True(FileMagicBytes.IsValidImage(header));
    }

    [Fact]
    public void IsValidImage_returns_false_for_pdf_bytes()
    {
        byte[] header = [0x25, 0x50, 0x44, 0x46, 0x00, 0x00]; // %PDF
        Assert.False(FileMagicBytes.IsValidImage(header));
    }

    // --- IsValidPdf ---

    [Fact]
    public void IsValidPdf_returns_true_for_pdf_header()
    {
        byte[] header = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31]; // %PDF-1
        Assert.True(FileMagicBytes.IsValidPdf(header));
    }

    [Fact]
    public void IsValidPdf_returns_false_for_jpeg_bytes()
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0x00];
        Assert.False(FileMagicBytes.IsValidPdf(header));
    }

    // --- IsValidZip ---

    [Fact]
    public void IsValidZip_returns_true_for_zip_header()
    {
        byte[] header = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]; // PK\x03\x04
        Assert.True(FileMagicBytes.IsValidZip(header));
    }

    [Fact]
    public void IsValidZip_returns_false_for_jpeg_bytes()
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0x00];
        Assert.False(FileMagicBytes.IsValidZip(header));
    }

    // --- ValidateAsync ---

    private static Stream MakeStream(byte[] bytes) => new MemoryStream(bytes);

    [Fact]
    public async Task ValidateAsync_returns_null_for_valid_jpeg()
    {
        byte[] data = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        var error = await FileMagicBytes.ValidateAsync(MakeStream(data), "jpg");
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateAsync_accepts_valid_image_bytes_regardless_of_specific_image_extension()
    {
        // PNG bytes under a .jpg extension should be accepted — we check "is it a real image?",
        // not "does the magic match the exact declared extension?"
        byte[] data = [0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        var error = await FileMagicBytes.ValidateAsync(MakeStream(data), "jpg");
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateAsync_returns_error_when_file_too_small()
    {
        byte[] data = [0xFF, 0xD8];
        var error = await FileMagicBytes.ValidateAsync(MakeStream(data), "jpg");
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ValidateAsync_resets_stream_position_to_zero_after_read()
    {
        byte[] data = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        var stream = MakeStream(data);

        await FileMagicBytes.ValidateAsync(stream, "jpg");

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ValidateAsync_returns_null_for_unknown_extension()
    {
        // Unknown extensions pass through without validation
        byte[] data = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B];
        var error = await FileMagicBytes.ValidateAsync(MakeStream(data), "mp4");
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateAsync_accepts_extension_with_leading_dot()
    {
        byte[] data = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        var error = await FileMagicBytes.ValidateAsync(MakeStream(data), ".jpeg");
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateAsync_returns_null_for_valid_pdf()
    {
        byte[] data = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x00, 0x00, 0x00, 0x00]; // %PDF-1.4
        var error = await FileMagicBytes.ValidateAsync(MakeStream(data), "pdf");
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateAsync_returns_null_for_valid_zip()
    {
        byte[] data = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]; // PK
        var error = await FileMagicBytes.ValidateAsync(MakeStream(data), "zip");
        Assert.Null(error);
    }
}
