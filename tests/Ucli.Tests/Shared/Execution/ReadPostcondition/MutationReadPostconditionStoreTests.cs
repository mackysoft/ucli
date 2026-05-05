using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

public sealed class MutationReadPostconditionStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOrNull_ReturnsNull_WhenFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "missing");
        var store = new MutationReadPostconditionStore();

        var result = await store.ReadOrNull(scope.FullPath, "fingerprint-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Null(result.ReadPostcondition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteMerged_ThenRead_MergesLatestRequirementPerKey ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "merge-roundtrip");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(scope.FullPath, "fingerprint-1");

        var firstWrite = await store.WriteMerged(
            scope.FullPath,
            "fingerprint-1",
            ReadPostconditionTestFactory.Create(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00")),
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"))
                {
                    ScenePath = @"Assets\Scenes\Main.unity",
                },
            ]),
            CancellationToken.None);
        var secondWrite = await store.WriteMerged(
            scope.FullPath,
            "fingerprint-1",
            ReadPostconditionTestFactory.Create(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00")),
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurfaceNames.GuidPath,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00")),
            ]),
            CancellationToken.None);

        Assert.True(firstWrite.IsSuccess);
        Assert.True(secondWrite.IsSuccess);

        var readResult = await store.ReadOrNull(scope.FullPath, "fingerprint-1", CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        var readPostcondition = Assert.IsType<OperationExecutionReadPostcondition>(readResult.ReadPostcondition);
        Assert.Equal(3, readPostcondition.Requirements.Count);
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => string.Equals(requirement.Surface, IpcExecuteReadPostconditionSurfaceNames.AssetSearch, StringComparison.Ordinal)
                && requirement.MinSafeGeneratedAtUtc == DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"));
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => string.Equals(requirement.Surface, IpcExecuteReadPostconditionSurfaceNames.GuidPath, StringComparison.Ordinal)
                && requirement.MinSafeGeneratedAtUtc == DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"));
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => string.Equals(requirement.Surface, IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite, StringComparison.Ordinal)
                && string.Equals(requirement.ScenePath, "Assets/Scenes/Main.unity", StringComparison.Ordinal));

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(documentPath));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("schemaVersion", 1)
            .HasArrayLength("requirements", 3);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOrNull_ReturnsInvalidArgument_WhenJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "malformed-json");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(scope.FullPath, "fingerprint-1");
        var relativePath = Path.GetRelativePath(scope.FullPath, documentPath);
        scope.WriteFile(relativePath, "{");

        var result = await store.ReadOrNull(scope.FullPath, "fingerprint-1", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
