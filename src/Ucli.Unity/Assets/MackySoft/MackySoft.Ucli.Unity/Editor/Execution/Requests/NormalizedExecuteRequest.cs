using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one normalized execute request used by execution pipelines. </summary>
    /// <param name="ProtocolVersion"> The validated protocol version. </param>
    /// <param name="RequestId"> The normalized request identifier. </param>
    /// <param name="Ops"> The normalized operation list. </param>
    /// <param name="PlanToken"> The optional plan token passed from CLI options. </param>
    /// <param name="CanonicalDigestPayloadUtf8"> The canonical UTF-8 JSON payload used as request-digest material. </param>
    internal sealed record NormalizedExecuteRequest (
        int ProtocolVersion,
        string RequestId,
        IReadOnlyList<NormalizedOperation> Ops,
        string? PlanToken,
        ReadOnlyMemory<byte> CanonicalDigestPayloadUtf8);
}
