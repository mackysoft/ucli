using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectAlreadyOpenLogMarkerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ExistsInFile_WhenEditorLogContainsUnityMarker_ReturnsTrue ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-already-open-log-marker", "marker");
        var editorLogPath = scope.WriteFile(
            "editor.log",
            "It looks like another Unity instance is running with this project open.");

        var result = UnityProjectAlreadyOpenLogMarker.ExistsInFile(editorLogPath);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExistsInFile_WhenEditorLogIsMissing_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-already-open-log-marker", "missing");

        var result = UnityProjectAlreadyOpenLogMarker.ExistsInFile(scope.GetPath("editor.log"));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExistsInText_WhenTextContainsUnityMarker_ReturnsTrue ()
    {
        var result = UnityProjectAlreadyOpenLogMarker.ExistsInText(
            "It looks like another Unity instance is running with this project open.");

        Assert.True(result);
    }
}
