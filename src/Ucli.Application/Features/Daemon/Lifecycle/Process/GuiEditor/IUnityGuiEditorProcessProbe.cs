namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.GuiEditor;

/// <summary> Verifies that one EditorInstance marker points to a live Unity GUI Editor process. </summary>
internal interface IUnityGuiEditorProcessProbe
{
    /// <summary> Verifies one marker candidate. </summary>
    ValueTask<UnityGuiEditorProcessProbeResult> ProbeAsync (
        UnityEditorInstanceMarker marker,
        CancellationToken cancellationToken = default);
}
