using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class UnityEditorInstanceMarkerReaderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenMarkerContainsOnlyProcessId_ReturnsMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-instance-marker-reader", "process-id-only");
        var unityProjectRoot = Path.Combine(scope.FullPath, "UnityProject");
        var libraryPath = Path.Combine(unityProjectRoot, "Library");
        Directory.CreateDirectory(libraryPath);
        var markerPath = Path.Combine(libraryPath, "EditorInstance.json");
        await File.WriteAllTextAsync(markerPath, """
        {
          "process_id": 1234
        }
        """);

        var reader = new UnityEditorInstanceMarkerReader();

        var result = await reader.ReadAsync(ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: unityProjectRoot,
            projectFingerprint: "fingerprint"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Exists);
        Assert.NotNull(result.Marker);
        Assert.Equal(markerPath, result.Marker!.MarkerPath);
        Assert.Equal(1234, result.Marker.ProcessId);
        Assert.Null(result.Marker.AppPath);
        Assert.Null(result.Marker.AppContentsPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenMarkerDoesNotExist_ReturnsNoMarker ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-instance-marker-reader", "missing");
        var unityProjectRoot = Path.Combine(scope.FullPath, "UnityProject");
        Directory.CreateDirectory(unityProjectRoot);
        var reader = new UnityEditorInstanceMarkerReader();

        var result = await reader.ReadAsync(ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: unityProjectRoot,
            projectFingerprint: "fingerprint"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Exists);
        Assert.Null(result.Marker);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenMarkerIsTooLarge_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-instance-marker-reader", "too-large");
        var unityProjectRoot = Path.Combine(scope.FullPath, "UnityProject");
        var libraryPath = Path.Combine(unityProjectRoot, "Library");
        Directory.CreateDirectory(libraryPath);
        var markerPath = Path.Combine(libraryPath, "EditorInstance.json");
        await File.WriteAllTextAsync(markerPath, new string(' ', 17 * 1024));
        var reader = new UnityEditorInstanceMarkerReader();

        var result = await reader.ReadAsync(ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: unityProjectRoot,
            projectFingerprint: "fingerprint"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("too large", result.Error.Message, StringComparison.Ordinal);
    }

}
