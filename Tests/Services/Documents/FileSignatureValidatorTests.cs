using dwa_ver_val.Services.Documents;
using Xunit;

namespace dwa_ver_val.Tests.Services.Documents;

public class FileSignatureValidatorTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    private static MemoryStream MakeStream(params byte[] bytes) => new(bytes);

    // PDF magic bytes: %PDF  (25 50 44 46)
    private static MemoryStream PdfBytes() => MakeStream(0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34);

    // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
    private static MemoryStream PngBytes() => MakeStream(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);

    // JPG magic bytes: FF D8 FF
    private static MemoryStream JpgBytes() => MakeStream(0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10);

    // TIFF little-endian: 49 49 2A 00
    private static MemoryStream TiffLeBytes() => MakeStream(0x49, 0x49, 0x2A, 0x00);

    // TIFF big-endian: 4D 4D 00 2A
    private static MemoryStream TiffBeBytes() => MakeStream(0x4D, 0x4D, 0x00, 0x2A);

    // plain text / garbage bytes
    private static MemoryStream HelloBytes() => MakeStream((byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o');

    // ── positive cases ───────────────────────────────────────────────────────

    [Fact]
    public void PdfBytes_WithPdfExtension_ReturnsTrue()
    {
        var stream = PdfBytes();
        Assert.True(FileSignatureValidator.MatchesExtension(stream, ".pdf"));
    }

    [Fact]
    public void PngBytes_WithPngExtension_ReturnsTrue()
    {
        var stream = PngBytes();
        Assert.True(FileSignatureValidator.MatchesExtension(stream, ".png"));
    }

    [Fact]
    public void JpgBytes_WithJpgExtension_ReturnsTrue()
    {
        var stream = JpgBytes();
        Assert.True(FileSignatureValidator.MatchesExtension(stream, ".jpg"));
    }

    [Fact]
    public void JpgBytes_WithJpegExtension_ReturnsTrue()
    {
        var stream = JpgBytes();
        Assert.True(FileSignatureValidator.MatchesExtension(stream, ".jpeg"));
    }

    [Fact]
    public void TiffLeBytes_WithTiffExtension_ReturnsTrue()
    {
        var stream = TiffLeBytes();
        Assert.True(FileSignatureValidator.MatchesExtension(stream, ".tiff"));
    }

    [Fact]
    public void TiffBeBytes_WithTiffExtension_ReturnsTrue()
    {
        var stream = TiffBeBytes();
        Assert.True(FileSignatureValidator.MatchesExtension(stream, ".tiff"));
    }

    // ── negative cases ───────────────────────────────────────────────────────

    [Fact]
    public void PngBytes_WithPdfExtension_ReturnsFalse()
    {
        var stream = PngBytes();
        Assert.False(FileSignatureValidator.MatchesExtension(stream, ".pdf"));
    }

    [Fact]
    public void HelloBytes_WithPdfExtension_ReturnsFalse()
    {
        var stream = HelloBytes();
        Assert.False(FileSignatureValidator.MatchesExtension(stream, ".pdf"));
    }

    [Fact]
    public void HelloBytes_WithPngExtension_ReturnsFalse()
    {
        var stream = HelloBytes();
        Assert.False(FileSignatureValidator.MatchesExtension(stream, ".png"));
    }

    [Fact]
    public void PdfBytes_WithPngExtension_ReturnsFalse()
    {
        var stream = PdfBytes();
        Assert.False(FileSignatureValidator.MatchesExtension(stream, ".png"));
    }

    [Fact]
    public void JpgBytes_WithPdfExtension_ReturnsFalse()
    {
        var stream = JpgBytes();
        Assert.False(FileSignatureValidator.MatchesExtension(stream, ".pdf"));
    }

    // ── unknown extension ────────────────────────────────────────────────────

    [Fact]
    public void UnknownExtension_ReturnsFalse()
    {
        var stream = PdfBytes();
        Assert.False(FileSignatureValidator.MatchesExtension(stream, ".exe"));
    }

    // ── stream position reset ────────────────────────────────────────────────

    [Fact]
    public void AfterCall_StreamPositionIsResetToZero()
    {
        var stream = PdfBytes();
        // advance position first
        stream.ReadByte();
        Assert.Equal(1, stream.Position);

        FileSignatureValidator.MatchesExtension(stream, ".pdf");

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void StreamPositionResetToZero_EvenOnMismatch()
    {
        var stream = PngBytes();
        stream.ReadByte();

        FileSignatureValidator.MatchesExtension(stream, ".pdf");

        Assert.Equal(0, stream.Position);
    }

    // ── case insensitivity of extension ──────────────────────────────────────

    [Fact]
    public void ExtensionMatching_IsCaseInsensitive()
    {
        var stream = PdfBytes();
        Assert.True(FileSignatureValidator.MatchesExtension(stream, ".PDF"));
    }
}
