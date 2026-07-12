using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one normalized execute request used by execution pipelines. </summary>
    /// <param name="SourceSteps"> The validated public source-step list preserved in request order for runtime compilation. </param>
    /// <param name="AllowDangerous"> Whether dangerous operations are explicitly allowed for call execution. </param>
    /// <param name="AllowPlayMode"> Whether Play Mode mutation is explicitly allowed for this request. </param>
    /// <param name="PlanToken"> The optional plan token passed from CLI options. </param>
    /// <param name="CanonicalDigestPayloadUtf8"> The canonical UTF-8 JSON payload used as request-digest material. </param>
    internal sealed record NormalizedExecuteRequest (
        IReadOnlyList<IpcExecuteStepContract> SourceSteps,
        bool AllowDangerous,
        bool AllowPlayMode,
        string? PlanToken,
        ReadOnlyMemory<byte> CanonicalDigestPayloadUtf8);
}
