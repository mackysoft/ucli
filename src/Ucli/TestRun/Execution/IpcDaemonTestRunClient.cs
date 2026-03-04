using System.Text.Json;
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
    private readonly IUnityIpcClient unityIpcClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonTestRunClient" /> class. </summary>
    /// <param name="unityIpcClient"> The Unity IPC client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public IpcDaemonTestRunClient (
        IUnityIpcClient unityIpcClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
    }

    /// <summary> Executes one Unity test run through daemon IPC and validates generated artifacts. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="timeout"> The IPC timeout used for one daemon request. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the Unity test execution result. </returns>
    public async ValueTask<UnityTestExecutionResult> Execute (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(artifactPaths);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            var sessionToken = await ResolveSessionToken(configuration, cancellationToken).ConfigureAwait(false);
            var request = CreateRequest(configuration, artifactPaths, sessionToken);
            var response = await unityIpcClient.SendAsync(
                    configuration.UnityProject.RepositoryRoot,
                    configuration.UnityProject.ProjectFingerprint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            var responseValidationResult = TryValidateResponse(response, out var responsePayload, out var errorMessage);
            if (!responseValidationResult)
            {
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.AbnormalExit,
                    errorMessage!);
            }

            if (!File.Exists(artifactPaths.ResultsXmlPath))
            {
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ArtifactMissing,
                    $"Unity daemon completed but results.xml was not generated: {artifactPaths.ResultsXmlPath}");
            }

            if (!File.Exists(artifactPaths.EditorLogPath))
            {
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ArtifactMissing,
                    $"Unity daemon completed but editor.log was not generated: {artifactPaths.EditorLogPath}");
            }

            return UnityTestExecutionResult.Success(responsePayload!.ExitCode);
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

    /// <summary> Creates one IPC request for daemon <c>test.run</c> execution. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="sessionToken"> The daemon session token. </param>
    /// <returns> The request envelope. </returns>
    private static IpcRequest CreateRequest (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        string sessionToken)
    {
        var payload = IpcPayloadCodec.SerializeToElement(
            new IpcTestRunRequest(
                TestPlatform: IpcTestRunPlatformCodec.ToValue(configuration.TestPlatform),
                BuildTarget: configuration.BuildTarget,
                TestFilter: configuration.TestFilter,
                TestCategories: configuration.TestCategories,
                AssemblyNames: configuration.AssemblyNames,
                TestSettingsPath: configuration.TestSettingsPath,
                ResultsXmlPath: artifactPaths.ResultsXmlPath,
                EditorLogPath: artifactPaths.EditorLogPath));
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"test-run-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: IpcMethodNames.TestRun,
            Payload: payload);
    }

    /// <summary> Validates one daemon response and decodes payload. </summary>
    /// <param name="response"> The daemon response envelope. </param>
    /// <param name="payload"> The decoded response payload when validation succeeds. </param>
    /// <param name="errorMessage"> The validation error when validation fails. </param>
    /// <returns> <see langword="true" /> when response is valid; otherwise <see langword="false" />. </returns>
    private static bool TryValidateResponse (
        IpcResponse response,
        out IpcTestRunResponse? payload,
        out string? errorMessage)
    {
        if (!string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal))
        {
            if (response.Errors.Count > 0)
            {
                var firstError = response.Errors[0];
                payload = null;
                errorMessage = $"Unity daemon test run failed with error code '{firstError.Code}'. {firstError.Message}";
                return false;
            }

            payload = null;
            errorMessage = $"Unity daemon test run failed with status '{response.Status}'.";
            return false;
        }

        if (response.Errors.Count > 0)
        {
            var firstError = response.Errors[0];
            payload = null;
            errorMessage = $"Unity daemon test run failed with error code '{firstError.Code}'. {firstError.Message}";
            return false;
        }

        if (!TryReadExitCode(response.Payload, out var exitCode, out var readError))
        {
            payload = null;
            errorMessage = $"Unity daemon test run payload is invalid. {readError}";
            return false;
        }

        if (exitCode != 0 && exitCode != 2)
        {
            payload = null;
            errorMessage = $"Unity daemon test run returned unsupported exit code: {exitCode}.";
            return false;
        }

        payload = new IpcTestRunResponse(exitCode);
        errorMessage = null;
        return true;
    }

    /// <summary> Reads required <c>exitCode</c> from daemon response payload. </summary>
    /// <param name="payload"> The response payload JSON element. </param>
    /// <param name="exitCode"> The parsed exit code value when read succeeds. </param>
    /// <param name="error"> The parse error message when read fails. </param>
    /// <returns> <see langword="true" /> when payload contains a valid integer <c>exitCode</c>; otherwise <see langword="false" />. </returns>
    private static bool TryReadExitCode (
        JsonElement payload,
        out int exitCode,
        out string? error)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            exitCode = default;
            error = "Response payload must be a JSON object.";
            return false;
        }

        if (!payload.TryGetProperty("exitCode", out var exitCodeElement))
        {
            exitCode = default;
            error = "Required property 'exitCode' is missing.";
            return false;
        }

        if (!exitCodeElement.TryGetInt32(out exitCode))
        {
            exitCode = default;
            error = "Property 'exitCode' must be an integer.";
            return false;
        }

        error = null;
        return true;
    }
}