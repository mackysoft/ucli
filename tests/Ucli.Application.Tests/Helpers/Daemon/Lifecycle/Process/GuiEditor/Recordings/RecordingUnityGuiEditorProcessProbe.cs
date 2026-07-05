namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingUnityGuiEditorProcessProbe : IUnityGuiEditorProcessProbe
{
    private readonly List<Invocation> invocations = [];

    public UnityGuiEditorProcessProbeResult Result { get; set; } =
        UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotRunning);

    public Action? OnProbe { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityGuiEditorProcessProbeResult> ProbeAsync (
        UnityEditorInstanceMarker marker,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OnProbe?.Invoke();
        invocations.Add(new Invocation(marker, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        UnityEditorInstanceMarker Marker,
        CancellationToken CancellationToken);
}
