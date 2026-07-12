namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Represents one parsed request root contract. </summary>
/// <param name="ProtocolVersion"> The parsed protocol version. </param>
/// <param name="Steps"> The parsed step list. </param>
internal sealed record IpcRequestContract (
    int ProtocolVersion,
    IReadOnlyList<IpcRequestContractStep?>? Steps);
