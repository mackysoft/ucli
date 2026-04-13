using MackySoft.Tests;
using MackySoft.Ucli.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class SceneTreeLiteSourceHashCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsStableHash_WhenSceneAndMetaAreUnchanged ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-hash", "stable");
        WriteScene(scope, sceneContents: "scene-v1", metaContents: "meta-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();

        var first = await calculator.TryCompute(scope.FullPath, "Assets/Scenes/Main.unity", CancellationToken.None);
        var second = await calculator.TryCompute(scope.FullPath, "Assets/Scenes/Main.unity", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsDifferentHash_WhenSceneContentsChange ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-hash", "scene-change");
        WriteScene(scope, sceneContents: "scene-v1", metaContents: "meta-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();
        var first = await calculator.TryCompute(scope.FullPath, "Assets/Scenes/Main.unity", CancellationToken.None);

        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity"), "scene-v2");
        var second = await calculator.TryCompute(scope.FullPath, "Assets/Scenes/Main.unity", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsDifferentHash_WhenMetaContentsChange ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-hash", "meta-change");
        WriteScene(scope, sceneContents: "scene-v1", metaContents: "meta-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();
        var first = await calculator.TryCompute(scope.FullPath, "Assets/Scenes/Main.unity", CancellationToken.None);

        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity.meta"), "meta-v2");
        var second = await calculator.TryCompute(scope.FullPath, "Assets/Scenes/Main.unity", CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsNull_WhenMetaFileIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-hash", "missing-meta");
        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity"), "scene-v1");
        var calculator = new SceneTreeLiteSourceHashCalculator();

        var result = await calculator.TryCompute(scope.FullPath, "Assets/Scenes/Main.unity", CancellationToken.None);

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
}