namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>gui.rebootstrap</c> IPC request payload. </summary>
/// <param name="ProjectFingerprint"> The project fingerprint expected by the caller. </param>
/// <param name="ReplaceExistingSession"> Whether the GUI process may replace an unreachable session owned by the same Editor instance. </param>
public sealed record IpcGuiRebootstrapRequest (
    string ProjectFingerprint,
    bool ReplaceExistingSession);
