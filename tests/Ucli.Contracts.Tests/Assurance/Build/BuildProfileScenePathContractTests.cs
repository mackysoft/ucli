using MackySoft.Ucli.Contracts.Assurance;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildProfileScenePathContractTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Assets/Scenes/Main.unity")]
    [InlineData("Assets/Main.unity")]
    public void IsProjectRelativeSceneAssetPath_WhenPathIsValid_ReturnsTrue (string path)
    {
        var result = BuildProfileScenePathContract.IsProjectRelativeSceneAssetPath(path);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Assets/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main.unity ")]
    [InlineData("Assets\\Scenes\\Main.unity")]
    [InlineData("Assets//Scenes/Main.unity")]
    [InlineData("Assets/./Scenes/Main.unity")]
    [InlineData("Assets/../Scenes/Main.unity")]
    [InlineData("/Assets/Scenes/Main.unity")]
    [InlineData("C:/Project/Assets/Scenes/Main.unity")]
    [InlineData("Packages/Scenes/Main.unity")]
    [InlineData("Assets/Scenes/Main.scene")]
    public void IsProjectRelativeSceneAssetPath_WhenPathIsInvalid_ReturnsFalse (string? path)
    {
        var result = BuildProfileScenePathContract.IsProjectRelativeSceneAssetPath(path);

        Assert.False(result);
    }
}
