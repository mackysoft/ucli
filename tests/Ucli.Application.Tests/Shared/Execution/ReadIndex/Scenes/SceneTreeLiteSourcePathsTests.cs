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

        Assert.Equal(sceneAssetPath, result.SceneAssetPath);
        Assert.Equal(project.UnityProjectRoot, result.SceneFilePath.BoundaryRoot);
        Assert.Equal("Assets/Scenes/Main.unity", result.SceneFilePath.RelativePath.Value);
        Assert.Equal(project.UnityProjectRoot, result.MetaFilePath.BoundaryRoot);
        Assert.Equal("Assets/Scenes/Main.unity.meta", result.MetaFilePath.RelativePath.Value);
    }
}
