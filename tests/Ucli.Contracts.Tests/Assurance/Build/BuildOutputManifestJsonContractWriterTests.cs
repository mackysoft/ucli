using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildOutputManifestJsonContractWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildOutputManifestEntryKind_HasStableContractLiterals ()
    {
        Assert.Equal("file", ContractLiteralCodec.ToValue(BuildOutputManifestEntryKind.File));
        Assert.Equal("directory", ContractLiteralCodec.ToValue(BuildOutputManifestEntryKind.Directory));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Write_WritesStablePublicShapeWithTrailingNewline ()
    {
        var writer = new BuildOutputManifestJsonContractWriter();
        var contract = new BuildOutputManifestJsonContract(
            BuildOutputManifestJsonContract.CurrentSchemaVersion,
            new BuildOutputManifestTargetJsonContract("standaloneLinux64", "StandaloneLinux64"),
            [
                new BuildOutputManifestEntryJsonContract("output-0001", "directory", "/workspace/build/player"),
            ],
            1,
            2,
            17,
            [
                new BuildOutputManifestFileJsonContract("output-0001", "output-0001/Data/config.json", "/workspace/build/player/Data/config.json", "output/output-0001/Data/config.json", 5, new string('a', 64)),
                new BuildOutputManifestFileJsonContract("output-0001", "output-0001/Game.x86_64", "/workspace/build/player/Game.x86_64", "output/output-0001/Game.x86_64", 12, new string('b', 64)),
            ],
            new string('c', 64));

        var json = writer.Write(contract);

        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            [
                "schemaVersion",
                "target",
                "entries",
                "entryCount",
                "fileCount",
                "totalBytes",
                "files",
                "manifestDigest",
            ],
            root.EnumerateObject().Select(static property => property.Name).ToArray());

        var target = root.GetProperty("target");
        Assert.Equal(
            [
                "stableName",
                "unityBuildTarget",
            ],
            target.EnumerateObject().Select(static property => property.Name).ToArray());

        var firstEntry = root.GetProperty("entries")[0];
        Assert.Equal(
            [
                "id",
                "kind",
                "sourcePath",
            ],
            firstEntry.EnumerateObject().Select(static property => property.Name).ToArray());

        var firstFile = root.GetProperty("files")[0];
        Assert.Equal(
            [
                "entryId",
                "logicalPath",
                "sourcePath",
                "artifactPath",
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
            new BuildOutputManifestTargetJsonContract("standaloneLinux64", "StandaloneLinux64"),
            [
                new BuildOutputManifestEntryJsonContract("output-0001", "file", "/workspace/build/player/Player"),
            ],
            1,
            1,
            12,
            [
                new BuildOutputManifestFileJsonContract("output-0001", "output-0001/Player", "/workspace/build/player/Player", "output/output-0001/Player", 12, new string('b', 64)),
            ]);

        var digestSource = writer.WriteDigestSource(content);
        var digest = writer.CalculateManifestDigest(content);

        Assert.Equal(
            "{\"schemaVersion\":1,\"target\":{\"stableName\":\"standaloneLinux64\",\"unityBuildTarget\":\"StandaloneLinux64\"},\"entries\":[{\"id\":\"output-0001\",\"kind\":\"file\",\"sourcePath\":\"/workspace/build/player/Player\"}],\"entryCount\":1,\"fileCount\":1,\"totalBytes\":12,\"files\":[{\"entryId\":\"output-0001\",\"logicalPath\":\"output-0001/Player\",\"sourcePath\":\"/workspace/build/player/Player\",\"artifactPath\":\"output/output-0001/Player\",\"sizeBytes\":12,\"sha256\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}]}",
            digestSource);
        Assert.Equal("d1280d23359f281aceca8d928adaca97dc3f9b940cdc30648fdf20edd4a216d3", digest);
        Assert.DoesNotContain("manifestDigest", digestSource, StringComparison.Ordinal);
        Assert.All(digest, static c => Assert.True(
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
            $"Character '{c}' is not lowercase hexadecimal."));
    }
}
