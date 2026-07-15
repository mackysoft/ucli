using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Hosting.Cli.Common.Serialization;

namespace MackySoft.Ucli.Tests;

public sealed class ReportOutputContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FromPath_WithDigest_SerializesPathAndDigestOnly ()
    {
        var digest = Sha256Digest.Parse(new string('a', 64));
        var report = AssuranceReportReference.FromPath("artifacts/report.json", digest);

        var json = Serialize(report);

        Assert.Equal("artifacts/report.json", json.GetProperty("path").GetString());
        Assert.Equal(digest.ToString(), json.GetProperty("digest").GetString());
        Assert.False(json.TryGetProperty("uri", out _));
        Assert.Equal(2, json.EnumerateObject().Count());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromUri_WithoutDigest_SerializesUriOnly ()
    {
        var report = AssuranceReportReference.FromUri("ucli://logs/unity?tail=200", digest: null);

        var json = Serialize(report);

        Assert.Equal("ucli://logs/unity?tail=200", json.GetProperty("uri").GetString());
        Assert.False(json.TryGetProperty("path", out _));
        Assert.False(json.TryGetProperty("digest", out _));
        Assert.Single(json.EnumerateObject());
    }

    private static JsonElement Serialize (object report)
    {
        return JsonSerializer.SerializeToElement(
            report,
            report.GetType(),
            CliOutputJsonSerializerOptions.Default);
    }
}
