using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests;

public sealed class UnityProjectAlreadyOpenLogClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ContainsAlreadyOpenMarker_WhenEditorLogContainsUnityMarker_ReturnsTrue ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-already-open-log-classifier", "marker");
        var editorLogPath = scope.WriteFile(
            "editor.log",
            "It looks like another Unity instance is running with this project open.");

        var result = UnityProjectAlreadyOpenLogClassifier.ContainsAlreadyOpenMarker(editorLogPath);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ContainsAlreadyOpenMarker_WhenEditorLogIsMissing_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-already-open-log-classifier", "missing");

        var result = UnityProjectAlreadyOpenLogClassifier.ContainsAlreadyOpenMarker(scope.GetPath("editor.log"));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ContainsAlreadyOpenMarkerInText_WhenTextContainsUnityMarker_ReturnsTrue ()
    {
        var result = UnityProjectAlreadyOpenLogClassifier.ContainsAlreadyOpenMarkerInText(
            "It looks like another Unity instance is running with this project open.");

        Assert.True(result);
    }
}
