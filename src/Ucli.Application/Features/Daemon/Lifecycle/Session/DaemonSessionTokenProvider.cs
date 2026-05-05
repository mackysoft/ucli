using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Resolves daemon session token values from persisted daemon session metadata. </summary>
internal sealed class DaemonSessionTokenProvider : IDaemonSessionTokenProvider
{
    private readonly IDaemonSessionStore daemonSessionStore;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionTokenProvider" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonSessionStore" /> is <see langword="null" />. </exception>
    public DaemonSessionTokenProvider (IDaemonSessionStore daemonSessionStore)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
    }

    /// <summary> Resolves one daemon session token value for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session token resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonSessionTokenResolutionResult> Resolve (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return DaemonSessionTokenResolutionResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            return DaemonSessionTokenResolutionResult.SessionNotAvailable();
        }

        var sessionToken = readResult.Session!.SessionToken;
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return DaemonSessionTokenResolutionResult.Failure(ExecutionError.InvalidArgument(
                "Daemon session token is missing."));
        }

        return DaemonSessionTokenResolutionResult.Success(sessionToken);
    }
}
