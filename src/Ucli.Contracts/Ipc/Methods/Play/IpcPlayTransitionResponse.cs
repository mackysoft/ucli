namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a Play Mode transition IPC response payload. </summary>
/// <param name="Transition"> The transition result details. </param>
public sealed record IpcPlayTransitionResponse (
    IpcPlayTransitionResult Transition);
