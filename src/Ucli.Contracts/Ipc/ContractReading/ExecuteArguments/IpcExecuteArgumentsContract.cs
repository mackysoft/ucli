namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Represents the parsed root contract of an <c>execute</c> request's arguments payload. </summary>
/// <param name="ProtocolVersion"> The parsed protocol version. </param>
/// <param name="Steps"> The parsed step list. </param>
internal sealed record IpcExecuteArgumentsContract (
    int ProtocolVersion,
    IReadOnlyList<IpcExecuteStepContract?>? Steps);
