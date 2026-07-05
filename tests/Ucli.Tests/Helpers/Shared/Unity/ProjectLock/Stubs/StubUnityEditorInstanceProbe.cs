using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class StubUnityEditorInstanceProbe : IUnityEditorInstanceProbe
{
    private readonly UnityEditorInstanceProbeResult result;

    public StubUnityEditorInstanceProbe (UnityEditorInstanceProbeResult result)
    {
        this.result = result;
    }

    public ValueTask<UnityEditorInstanceProbeResult> ProbeAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}
