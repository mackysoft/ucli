using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class MutationReadPostconditionStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOrNull_ReturnsNull_WhenFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "missing");
        var store = new MutationReadPostconditionStore();

        var result = await store.ReadOrNullAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Null(result.ReadPostcondition);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteMerged_ThenRead_MergesLatestRequirementPerKey ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "merge-roundtrip");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-1"));

        var firstWrite = await store.WriteMergedAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-1"),
            new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurface.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: null),
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurface.SceneTreeLite,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: new UnityScenePath(@"Assets\Scenes\Main.unity")),
            ]),
            CancellationToken.None);
        var secondWrite = await store.WriteMergedAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-1"),
            new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurface.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"),
                    ScenePath: null),
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurface.GuidPath,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"),
                    ScenePath: null),
            ]),
            CancellationToken.None);

        Assert.True(firstWrite.IsSuccess);
        Assert.True(secondWrite.IsSuccess);

        var readResult = await store.ReadOrNullAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        var readPostcondition = Assert.IsType<IpcExecuteReadPostcondition>(readResult.ReadPostcondition);
        Assert.Equal(3, readPostcondition.Requirements.Count);
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => requirement.Surface == IpcExecuteReadPostconditionSurface.AssetSearch
                && requirement.MinSafeGeneratedAtUtc == DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"));
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => requirement.Surface == IpcExecuteReadPostconditionSurface.GuidPath
                && requirement.MinSafeGeneratedAtUtc == DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"));
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => requirement.Surface == IpcExecuteReadPostconditionSurface.SceneTreeLite
                && requirement.ScenePath == new UnityScenePath("Assets/Scenes/Main.unity"));

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(documentPath));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("schemaVersion", 1)
            .HasArrayLength("requirements", 3);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteMerged_WhenSceneTreeLiteHasNoScenePath_PersistsWildcardRequirement ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "scene-tree-lite-wildcard");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-1"));

        var writeResult = await store.WriteMergedAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint-1"),
            new IpcExecuteReadPostcondition(
            [
                new IpcExecuteReadPostconditionRequirement(
                    Surface: IpcExecuteReadPostconditionSurface.SceneTreeLite,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: null),
            ]),
            CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var readResult = await store.ReadOrNullAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        var readPostcondition = Assert.IsType<IpcExecuteReadPostcondition>(readResult.ReadPostcondition);
        var requirement = Assert.Single(readPostcondition.Requirements);
        Assert.Equal(IpcExecuteReadPostconditionSurface.SceneTreeLite, requirement.Surface);
        Assert.Null(requirement.ScenePath);

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(documentPath));
        Assert.False(jsonDocument.RootElement.GetProperty("requirements")[0].TryGetProperty("scenePath", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOrNull_ReturnsInvalidArgument_WhenJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "malformed-json");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-1"));
        var relativePath = Path.GetRelativePath(scope.FullPath, documentPath);
        scope.WriteFile(relativePath, "{");

        var result = await store.ReadOrNullAsync(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
