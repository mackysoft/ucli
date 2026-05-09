using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;

/// <summary> Implements cleanup compensation for failed daemon launch attempts. </summary>
internal sealed class DaemonLaunchCompensationService : IDaemonLaunchCompensationService
{
    private readonly IDaemonProcessTerminationService processTerminationService;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchCompensationService" /> class. </summary>
    /// <param name="processTerminationService"> The process-termination service dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchCompensationService (
        IDaemonProcessTerminationService processTerminationService,
        IDaemonArtifactCleaner artifactCleaner)
    {
        this.processTerminationService = processTerminationService ?? throw new ArgumentNullException(nameof(processTerminationService));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
    }

    /// <summary> Stops the launched process snapshot and cleans daemon artifacts after launch failure. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="target"> The launched process termination target when available. </param>
    /// <param name="timeout"> The remaining timeout budget for launch-failure compensation. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The compensation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget? target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var compensationTimeout = timeout > DaemonTimeouts.LaunchCompensationTimeout
            ? DaemonTimeouts.LaunchCompensationTimeout
            : timeout;

        var stopResult = await processTerminationService.EnsureStoppedAsync(
                target,
                compensationTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!stopResult.IsSuccess)
        {
            return stopResult;
        }

        return await artifactCleaner.CleanupAsync(unityProject, cancellationToken).ConfigureAwait(false);
    }
}
