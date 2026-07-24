using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Scenes;

public sealed class FileSceneTreeLiteSourceProbeTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureCurrentAssetsSceneExists_ReturnsSuccess_WhenGuardedSceneFileExists ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-source-probe", "exists");
        scope.WriteFile(Path.Combine("Assets", "Scenes", "Main.unity"), "scene");
        var sourcePaths = SceneTreeLiteSourcePaths.Create(
            AbsolutePath.Parse(scope.FullPath),
            new SceneAssetPath("Assets/Scenes/Main.unity"));
        var probe = new FileSceneTreeLiteSourceProbe();

        var result = await probe.EnsureCurrentAssetsSceneExistsAsync(sourcePaths, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureCurrentAssetsSceneExists_ReturnsFailure_WhenGuardedSceneFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("scene-tree-lite-source-probe", "missing");
        var sourcePaths = SceneTreeLiteSourcePaths.Create(
            AbsolutePath.Parse(scope.FullPath),
            new SceneAssetPath("Assets/Scenes/Main.unity"));
        var probe = new FileSceneTreeLiteSourceProbe();

        var result = await probe.EnsureCurrentAssetsSceneExistsAsync(sourcePaths, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Assets/Scenes/Main.unity", result.ErrorMessage, StringComparison.Ordinal);
    }
}
