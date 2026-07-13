using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactReaderOpsTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOpsCatalog_ReturnsContract_WhenCatalogExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-success");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexOpsCatalogEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    Description: "Returns a GameObject description.",
                    DescribeKey: new string('a', 64),
                    DescribeHash: new string('b', 64)),
            ]);
        FileReadIndexArtifactReaderTestSupport.WriteText(
            UcliStoragePathResolver.ResolveOpsCatalogPath(scope.FullPath, fingerprint),
            FileReadIndexArtifactReaderTestSupport.Write(contract));

        var result = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Equal("Returns a GameObject description.", result.Value.Entries[0].Description);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOpsDescribe_ReturnsContract_WhenDetailExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-success");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        const string sourceInputsHash = "source-hash";
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var operation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var catalogEntry = FileReadIndexArtifactReaderTestSupport.WriteOpsDescribe(scope.FullPath, fingerprint, operation, sourceInputsHash);

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, sourceInputsHash, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sourceInputsHash, result.Value.SourceInputsHash);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, result.Value.Operation!.Name);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOpsDescribe_ReturnsBootstrapFailed_WhenDetailDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-missing");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var catalogEntry = new IndexOpsCatalogEntryJsonContract(
            UcliPrimitiveOperationNames.GoDescribe,
            "query",
            "safe",
            "Returns a GameObject description.",
            new string('a', 64),
            new string('b', 64));

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOpsDescribe_ReturnsFormatInvalid_WhenDescribeHashDoesNotMatch ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-hash-mismatch");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var operation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var catalogEntry = FileReadIndexArtifactReaderTestSupport.WriteOpsDescribe(scope.FullPath, fingerprint, operation, "source-hash");
        catalogEntry = catalogEntry with { DescribeHash = new string('0', 64) };

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
        Assert.Contains("describeHash", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOpsDescribe_ReturnsFormatInvalid_WhenSourceInputsHashDoesNotMatch ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-source-mismatch");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var operation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var catalogEntry = FileReadIndexArtifactReaderTestSupport.WriteOpsDescribe(scope.FullPath, fingerprint, operation, "other-source-hash");

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
        Assert.Contains("sourceInputsHash", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOpsDescribe_ReturnsFormatInvalid_WhenOperationDescriptorDoesNotMatch ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-descriptor-mismatch");
        var reader = new FileReadIndexArtifactReader();
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var operation = ReadIndexOperationTestFactory.CreateGoDescribeEntry() with { Name = "ucli.test.detail" };
        var catalogEntry = FileReadIndexArtifactReaderTestSupport.WriteOpsDescribe(scope.FullPath, fingerprint, operation, "source-hash");
        catalogEntry = catalogEntry with { Name = UcliPrimitiveOperationNames.GoDescribe };

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
        Assert.Contains("operation descriptor", result.Error.Message, StringComparison.Ordinal);
    }
}
