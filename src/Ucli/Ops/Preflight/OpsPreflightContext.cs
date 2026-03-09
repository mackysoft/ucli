using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Ops.Preflight;

/// <summary> Represents preflight-resolved execution context for ops catalog access. </summary>
/// <param name="Context"> The resolved shared project context. </param>
/// <param name="ReadIndexMode"> The resolved read-index mode. </param>
internal sealed record OpsPreflightContext (
    ProjectContext Context,
    ReadIndexMode ReadIndexMode);