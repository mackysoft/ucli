using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.Infrastructure.Tests.Index.Scenes;

public sealed class SceneTreeLiteSourceHashCalculatorTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryCompute_ReturnsStableHash_WhenSceneAndMetaAreUnchanged ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-scene-tree-lite-hash", "stable");
        WriteScene(scope, sceneContents: "scene-v1", metaContents: "meta-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();
        var projectRoot = AbsolutePath.Parse(scope.FullPath);
        var sourcePaths = CreateSourcePaths(projectRoot);

        var first = await calculator.TryComputeAsync(sourcePaths.Scene, sourcePaths.Meta, CancellationToken.None);
        var second = await calculator.TryComputeAsync(sourcePaths.Scene, sourcePaths.Meta, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryCompute_ReturnsDifferentHash_WhenSceneContentsChange ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-scene-tree-lite-hash", "scene-change");
        WriteScene(scope, sceneContents: "scene-v1", metaContents: "meta-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();
        var projectRoot = AbsolutePath.Parse(scope.FullPath);
        var sourcePaths = CreateSourcePaths(projectRoot);
        var first = await calculator.TryComputeAsync(sourcePaths.Scene, sourcePaths.Meta, CancellationToken.None);

        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity"), "scene-v2");
        var second = await calculator.TryComputeAsync(sourcePaths.Scene, sourcePaths.Meta, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryCompute_ReturnsDifferentHash_WhenMetaContentsChange ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-scene-tree-lite-hash", "meta-change");
        WriteScene(scope, sceneContents: "scene-v1", metaContents: "meta-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();
        var projectRoot = AbsolutePath.Parse(scope.FullPath);
        var sourcePaths = CreateSourcePaths(projectRoot);
        var first = await calculator.TryComputeAsync(sourcePaths.Scene, sourcePaths.Meta, CancellationToken.None);

        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity.meta"), "meta-v2");
        var second = await calculator.TryComputeAsync(sourcePaths.Scene, sourcePaths.Meta, CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryCompute_ReturnsNull_WhenMetaFileIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-scene-tree-lite-hash", "missing-meta");
        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity"), "scene-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();
        var projectRoot = AbsolutePath.Parse(scope.FullPath);
        var sourcePaths = CreateSourcePaths(projectRoot);

        var result = await calculator.TryComputeAsync(sourcePaths.Scene, sourcePaths.Meta, CancellationToken.None);

        Assert.Null(result);
    }

    private static void WriteScene (
        TestDirectoryScope scope,
        string sceneContents,
        string metaContents)
    {
        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity"), sceneContents);
        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity.meta"), metaContents);
    }

    private static (ContainedPath Scene, ContainedPath Meta) CreateSourcePaths (AbsolutePath projectRoot)
    {
        return (
            ContainedPath.Create(projectRoot, RootRelativePath.Parse("Assets/Scenes/Main.unity")),
            ContainedPath.Create(projectRoot, RootRelativePath.Parse("Assets/Scenes/Main.unity.meta")));
    }
}
