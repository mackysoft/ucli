namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>gui.rebootstrap</c> IPC request payload. </summary>
/// <param name="ProjectFingerprint"> The project fingerprint expected by the caller. </param>
public sealed record IpcGuiRebootstrapRequest (
    string ProjectFingerprint);
