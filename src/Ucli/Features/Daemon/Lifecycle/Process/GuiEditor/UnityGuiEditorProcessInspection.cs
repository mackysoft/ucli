namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.GuiEditor;

/// <summary> Represents inspected process metadata used to verify a GUI Unity Editor marker candidate. </summary>
internal sealed record UnityGuiEditorProcessInspection (
    bool Exists,
    bool HasExited,
    DateTimeOffset? StartTimeUtc,
    string? ProcessName,
    string? CommandLine,
    string? ExecutablePath,
    bool? IsOwnedByCurrentUser)
{
    /// <summary> Creates a not-running inspection result. </summary>
    public static UnityGuiEditorProcessInspection NotRunning ()
    {
        return new UnityGuiEditorProcessInspection(
            Exists: false,
            HasExited: true,
            StartTimeUtc: null,
            ProcessName: null,
            CommandLine: null,
            ExecutablePath: null,
            IsOwnedByCurrentUser: null);
    }
}
