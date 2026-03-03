using MackySoft.Ucli.Context;

namespace MackySoft.Ucli.Status;

/// <summary> Represents normalized preflight values for one status execution. </summary>
/// <param name="Context"> The resolved init/status context. </param>
/// <param name="Timeout"> The effective timeout used for daemon probing. </param>
/// <param name="UnityVersion"> The Unity version resolved from <c>ProjectVersion.txt</c>. </param>
internal sealed record StatusExecutionContext (
    InitStatusContext Context,
    TimeSpan Timeout,
    string UnityVersion);