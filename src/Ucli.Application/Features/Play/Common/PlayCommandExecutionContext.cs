using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary> Represents the resolved execution context shared by Play Mode lifecycle commands. </summary>
/// <param name="ProjectContext"> The resolved project context. </param>
/// <param name="Project"> The public project identity. </param>
/// <param name="Session"> The registered GUI daemon session. </param>
/// <param name="Timeout"> The effective command timeout. </param>
/// <param name="TimeoutMilliseconds"> The effective command timeout in milliseconds. </param>
internal sealed record PlayCommandExecutionContext (
    ProjectContext ProjectContext,
    ProjectIdentityInfo Project,
    DaemonSession Session,
    TimeSpan Timeout,
    int TimeoutMilliseconds);
