using System.Collections.Generic;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one parsed request root contract. </summary>
/// <param name="ProtocolVersion"> The parsed protocol version. </param>
/// <param name="RequestId"> The parsed request identifier. </param>
/// <param name="Steps"> The parsed step list. </param>
internal sealed record IpcRequestContract (
    int ProtocolVersion,
    string? RequestId,
    IReadOnlyList<IpcRequestContractStep?>? Steps);