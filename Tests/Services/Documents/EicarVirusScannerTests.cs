using System.Text;
using dwa_ver_val.Services.Documents;
using Xunit;

namespace dwa_ver_val.Tests.Services.Documents;

public class EicarVirusScannerTests
{
    private static readonly string EicarString =
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";

    [Fact]
    public async Task ScanAsync_ReturnsInfected_WhenContentIsExactEicarSignature()
    {
        var scanner = new EicarVirusScanner();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(EicarString));

        var result = await scanner.ScanAsync(stream, CancellationToken.None);

        Assert.Equal(VirusScanResult.Infected, result);
    }

    [Fact]
    public async Task ScanAsync_ReturnsInfected_WhenEicarSignatureEmbeddedInPdf()
    {
        // The E2E payload shape: a valid PDF header with EICAR embedded in the body.
        var pdf = "%PDF-1.4\n" + EicarString + "\n%%EOF";
        var scanner = new EicarVirusScanner();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(pdf));

        var result = await scanner.ScanAsync(stream, CancellationToken.None);

        Assert.Equal(VirusScanResult.Infected, result);
    }

    [Fact]
    public async Task ScanAsync_ReturnsClean_WhenContentIsBenign()
    {
        var scanner = new EicarVirusScanner();
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nhello world\n%%EOF"));

        var result = await scanner.ScanAsync(stream, CancellationToken.None);

        Assert.Equal(VirusScanResult.Clean, result);
    }

    [Fact]
    public async Task ScanAsync_ReturnsClean_WhenContentIsEmpty()
    {
        var scanner = new EicarVirusScanner();
        using var stream = new MemoryStream(Array.Empty<byte>());

        var result = await scanner.ScanAsync(stream, CancellationToken.None);

        Assert.Equal(VirusScanResult.Clean, result);
    }

    [Fact]
    public async Task ScanAsync_ResetsStreamPosition_SoCallerCanReadFullContent()
    {
        var scanner = new EicarVirusScanner();
        var payload = Encoding.ASCII.GetBytes("%PDF-1.4\nclean\n%%EOF");
        using var stream = new MemoryStream(payload);

        await scanner.ScanAsync(stream, CancellationToken.None);

        Assert.Equal(0, stream.Position);
        var roundTrip = new byte[payload.Length];
        var read = stream.Read(roundTrip, 0, roundTrip.Length);
        Assert.Equal(payload.Length, read);
        Assert.Equal(payload, roundTrip);
    }
}
