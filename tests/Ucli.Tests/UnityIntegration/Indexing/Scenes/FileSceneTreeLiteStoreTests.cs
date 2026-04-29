using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class FileSceneTreeLiteStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_CreatesLookupAtHashedScenePath ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-store", "write");
        var store = new FileSceneTreeLiteStore();
        var generatedAtUtc = DateTimeOffset.Parse("2026-04-14T00:00:00+00:00");

        await store.Write(
            scope.FullPath,
            "project-fingerprint",
            generatedAtUtc,
            "Assets\\Scenes\\Main.unity",
            CreateRoots("Root"),
            "scene-hash",
            CancellationToken.None);

        var lookupPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
            scope.FullPath,
            "project-fingerprint",
            "Assets/Scenes/Main.unity");

        Assert.True(File.Exists(lookupPath));

        var reader = new FileIndexCatalogReader();
        var result = await reader.ReadSceneTreeLiteLookup(
            scope.FullPath,
            "project-fingerprint",
            "Assets/Scenes/Main.unity",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(generatedAtUtc, result.Value!.GeneratedAtUtc);
        Assert.Equal("Assets/Scenes/Main.unity", result.Value.ScenePath);
        Assert.Equal("scene-hash", result.Value.SourceInputsHash);
        Assert.Single(result.Value.Roots!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Write_OverwritesOnlyTargetSceneLookup ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-store", "overwrite");
        var store = new FileSceneTreeLiteStore();
        var reader = new FileIndexCatalogReader();

        await store.Write(
            scope.FullPath,
            "project-fingerprint",
            DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
            "Assets/Scenes/First.unity",
            CreateRoots("First-v1"),
            "first-hash-v1",
            CancellationToken.None);
        await store.Write(
            scope.FullPath,
            "project-fingerprint",
            DateTimeOffset.Parse("2026-04-14T00:01:00+00:00"),
            "Assets/Scenes/Second.unity",
            CreateRoots("Second"),
            "second-hash",
            CancellationToken.None);
        await store.Write(
            scope.FullPath,
            "project-fingerprint",
            DateTimeOffset.Parse("2026-04-14T00:02:00+00:00"),
            "Assets/Scenes/First.unity",
            CreateRoots("First-v2"),
            "first-hash-v2",
            CancellationToken.None);

        var firstResult = await reader.ReadSceneTreeLiteLookup(
            scope.FullPath,
            "project-fingerprint",
            "Assets/Scenes/First.unity",
            CancellationToken.None);
        var secondResult = await reader.ReadSceneTreeLiteLookup(
            scope.FullPath,
            "project-fingerprint",
            "Assets/Scenes/Second.unity",
            CancellationToken.None);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal("first-hash-v2", firstResult.Value!.SourceInputsHash);
        Assert.Equal("First-v2", firstResult.Value.Roots![0].Name);
        Assert.Equal("second-hash", secondResult.Value!.SourceInputsHash);
        Assert.Equal("Second", secondResult.Value.Roots![0].Name);
    }

    private static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> CreateRoots (string rootName)
    {
        return
        [
            new IndexSceneTreeLiteNodeJsonContract(
                rootName,
                "GlobalObjectId_V1-2-3-4",
                Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
        ];
    }
}
