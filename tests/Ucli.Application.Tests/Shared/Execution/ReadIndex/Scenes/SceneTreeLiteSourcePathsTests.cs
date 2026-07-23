using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

public sealed class SceneTreeLiteSourcePathsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_DerivesSceneAndMetaPathsWithinTheProjectBoundary ()
    {
        var project = ProjectContextTestFactory.CreateUnknownVersionUnityProject();
        var sceneAssetPath = new SceneAssetPath("Assets/Scenes/Main.unity");

        var result = SceneTreeLiteSourcePaths.Create(project.UnityProjectRoot, sceneAssetPath);

        Assert.Same(sceneAssetPath, result.SceneAssetPath);
        Assert.Same(project.UnityProjectRoot, result.SceneFilePath.BoundaryRoot);
        Assert.Equal("Assets/Scenes/Main.unity", result.SceneFilePath.RelativePath.Value);
        Assert.Same(project.UnityProjectRoot, result.MetaFilePath.BoundaryRoot);
        Assert.Equal("Assets/Scenes/Main.unity.meta", result.MetaFilePath.RelativePath.Value);
    }
}
