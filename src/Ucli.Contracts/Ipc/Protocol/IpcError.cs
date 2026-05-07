namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one machine-readable IPC error entry. </summary>
/// <param name="Code"> The error code that identifies failure type. </param>
/// <param name="Message"> The human-readable error message. </param>
/// <param name="OpId"> The related operation identifier, or <see langword="null" /> when not applicable. </param>
public sealed record IpcError (
    UcliErrorCode Code,
    string Message,
    string? OpId);
