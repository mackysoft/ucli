using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Represents inspected process metadata used to verify a GUI Unity Editor marker candidate. </summary>
internal sealed record UnityGuiEditorProcessInspection (
    bool Exists,
    bool HasExited,
    DateTimeOffset? StartTimeUtc,
    string? ProcessName,
    string? CommandLine,
    string? ExecutablePath,
    bool? IsOwnedByCurrentUser,
    ExecutionError? Error)
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
            IsOwnedByCurrentUser: null,
            Error: null);
    }
}
