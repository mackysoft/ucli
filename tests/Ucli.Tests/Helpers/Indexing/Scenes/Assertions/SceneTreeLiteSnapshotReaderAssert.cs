using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;

internal static class SceneTreeLiteSnapshotReaderAssert
{
    public static RecordingSceneTreeLiteSnapshotReader.Invocation ReadRequested (
        RecordingSceneTreeLiteSnapshotReader reader,
        UnityExecutionMode expectedMode,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(reader.Invocations);
        Assert.Equal(expectedMode, invocation.Mode);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        return invocation;
    }

    public static RecordingSceneTreeLiteSnapshotReader.Invocation LoadedSceneProbeRequested (
        RecordingSceneTreeLiteSnapshotReader reader,
        string expectedScenePath)
    {
        var invocation = ReadRequested(reader, UnityExecutionMode.Daemon, expectedFailFast: true);
        Assert.Equal(expectedScenePath, invocation.ScenePath);
        Assert.True(invocation.LoadedSceneOnly);
        return invocation;
    }
}
