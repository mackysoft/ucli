using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Ops.Preflight;

/// <summary> Represents preflight-resolved execution context for ops catalog access. </summary>
/// <param name="Context"> The resolved shared project context. </param>
/// <param name="ReadIndexMode"> The resolved read-index mode. </param>
/// <param name="Mode"> The normalized Unity execution mode. </param>
/// <param name="Timeout"> The resolved command timeout for ops catalog access. </param>
/// <param name="FailFast"> Whether live source fallback should fail immediately instead of waiting for Unity readiness. </param>
internal sealed record OpsPreflightContext (
    ProjectContext Context,
    ReadIndexMode ReadIndexMode,
    UnityExecutionMode Mode,
    TimeSpan Timeout,
    bool FailFast);