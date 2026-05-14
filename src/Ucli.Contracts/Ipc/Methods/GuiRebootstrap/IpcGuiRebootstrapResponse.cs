namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>gui.rebootstrap</c> IPC response payload. </summary>
/// <param name="Accepted"> Whether the rebootstrap request was accepted. </param>
/// <param name="ProjectFingerprint"> The project fingerprint served by the GUI supervisor. </param>
/// <param name="ProcessId"> The Unity GUI process identifier. </param>
public sealed record IpcGuiRebootstrapResponse (
    bool Accepted,
    string ProjectFingerprint,
    int ProcessId);
