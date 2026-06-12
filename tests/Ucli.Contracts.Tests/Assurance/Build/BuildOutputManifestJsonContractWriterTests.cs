using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildOutputManifestJsonContractWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Write_WritesStablePublicShapeWithTrailingNewline ()
    {
        var writer = new BuildOutputManifestJsonContractWriter();
        var contract = new BuildOutputManifestJsonContract(
            BuildOutputManifestJsonContract.CurrentSchemaVersion,
            ".ucli/local/fingerprints/fingerprint/artifacts/build/run-1/output",
            "standaloneLinux64",
            2,
            17,
            [
                new BuildOutputManifestFileJsonContract("Data/config.json", 5, new string('a', 64)),
                new BuildOutputManifestFileJsonContract("Game.x86_64", 12, new string('b', 64)),
            ],
            new string('c', 64));

        var json = writer.Write(contract);

        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            [
                "schemaVersion",
                "outputRoot",
                "target",
                "fileCount",
                "totalBytes",
                "files",
                "manifestDigest",
            ],
            root.EnumerateObject().Select(static property => property.Name).ToArray());

        var firstFile = root.GetProperty("files")[0];
        Assert.Equal(
            [
                "path",
                "sizeBytes",
                "sha256",
            ],
            firstFile.EnumerateObject().Select(static property => property.Name).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CalculateManifestDigest_UsesCanonicalContentWithoutManifestDigest ()
    {
        var writer = new BuildOutputManifestJsonContractWriter();
        var content = new BuildOutputManifestContentJsonContract(
            BuildOutputManifestJsonContract.CurrentSchemaVersion,
            ".ucli/local/fingerprints/fingerprint/artifacts/build/run-1/output",
            "standaloneLinux64",
            1,
            12,
            [
                new BuildOutputManifestFileJsonContract("Game.x86_64", 12, new string('b', 64)),
            ]);

        var digestSource = writer.WriteDigestSource(content);
        var digest = writer.CalculateManifestDigest(content);

        Assert.Equal(
            "{\"schemaVersion\":1,\"outputRoot\":\".ucli/local/fingerprints/fingerprint/artifacts/build/run-1/output\",\"target\":\"standaloneLinux64\",\"fileCount\":1,\"totalBytes\":12,\"files\":[{\"path\":\"Game.x86_64\",\"sizeBytes\":12,\"sha256\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}]}",
            digestSource);
        Assert.Equal("a853dad0d38bd6ae09f37d3dc13c77616975011bc81076765bc274f41b9beebd", digest);
        Assert.DoesNotContain("manifestDigest", digestSource, StringComparison.Ordinal);
        Assert.All(digest, static c => Assert.True(
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
            $"Character '{c}' is not lowercase hexadecimal."));
    }
}
