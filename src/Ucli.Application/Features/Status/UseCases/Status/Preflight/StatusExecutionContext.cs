using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Preflight;

/// <summary> Represents normalized preflight values for one status execution. </summary>
/// <param name="Context"> The resolved shared project context. </param>
/// <param name="Timeout"> The effective timeout used for daemon probing. </param>
/// <param name="UnityVersion"> The Unity version resolved from <c>ProjectVersion.txt</c>. </param>
internal sealed record StatusExecutionContext (
    ProjectContext Context,
    TimeSpan Timeout,
    string UnityVersion);
