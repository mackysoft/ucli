using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one normalized execute request used by execution pipelines. </summary>
    /// <param name="ProtocolVersion"> The validated protocol version. </param>
    /// <param name="RequestId"> The normalized request identifier. </param>
    /// <param name="SourceSteps"> The validated public source-step list preserved in request order for runtime compilation. </param>
    /// <param name="PlanToken"> The optional plan token passed from CLI options. </param>
    /// <param name="CanonicalDigestPayloadUtf8"> The canonical UTF-8 JSON payload used as request-digest material. </param>
    internal sealed record NormalizedExecuteRequest (
        int ProtocolVersion,
        string RequestId,
        IReadOnlyList<IpcRequestContractStep> SourceSteps,
        string? PlanToken,
        ReadOnlyMemory<byte> CanonicalDigestPayloadUtf8);
}
