using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Implements daemon-backed Unity test-run execution over IPC transport. </summary>
internal sealed class IpcDaemonTestRunClient : IDaemonTestRunClient
{
    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonTestRunClient" /> class. </summary>
    /// <param name="transportClient"> The shared Unity IPC transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public IpcDaemonTestRunClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
    }

    /// <summary> Executes one Unity test run through daemon IPC and validates generated artifacts. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="timeout"> The IPC timeout used for one daemon request. </param>
    /// <param name="waitUntilReady"> Whether daemon execution may wait for lifecycle readiness before failing. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the Unity test execution result. </returns>
    public async ValueTask<UnityTestExecutionResult> Execute (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        bool waitUntilReady,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(artifactPaths);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            var sessionToken = await ResolveSessionToken(configuration, cancellationToken).ConfigureAwait(false);
            var request = IpcDaemonTestRunRequestCodec.CreateRequest(configuration, artifactPaths, sessionToken, waitUntilReady);
            var response = await transportClient.SendAsync(
                    configuration.UnityProject.RepositoryRoot,
                    configuration.UnityProject.ProjectFingerprint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IpcDaemonTestRunResponseCodec.TryDecode(response, out var exitCode, out var errorCode, out var errorMessage))
            {
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.AbnormalExit,
                    errorMessage!,
                    errorCode);
            }

            if (!TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var artifactValidationError))
            {
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ArtifactMissing,
                    artifactValidationError!);
            }

            return UnityTestExecutionResult.Success(exitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.Canceled,
                "Unity daemon test run execution was canceled.");
        }
        catch (TimeoutException)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.TimedOut,
                $"Unity daemon test run request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.StartFailed,
                $"Unity daemon is not running. {exception.Message}");
        }
        catch (Exception exception)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                $"Unexpected error during Unity daemon test run execution: {exception.Message}");
        }
    }

    /// <summary> Resolves the daemon session token from local daemon session storage. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to session token value. </returns>
    private async ValueTask<string> ResolveSessionToken (
        ResolvedTestRunConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var sessionTokenResult = await daemonSessionTokenProvider.Resolve(
                configuration.UnityProject,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sessionTokenResult.IsSuccess)
        {
            var message = sessionTokenResult.IsSessionNotAvailable
                ? "Daemon session token is not available."
                : $"Daemon session token could not be resolved. {sessionTokenResult.Error!.Message}";
            throw new InvalidOperationException(message);
        }

        return sessionTokenResult.Token!;
    }
}