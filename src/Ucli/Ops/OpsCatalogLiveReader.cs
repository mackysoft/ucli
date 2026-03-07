using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Index;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;

namespace MackySoft.Ucli.Ops;

/// <summary> Reads the live operation catalog from Unity daemon or oneshot batchmode execution. </summary>
internal sealed class OpsCatalogLiveReader : IOpsCatalogLiveReader
{
    private readonly IUnityIpcClient unityIpcClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    private readonly IUnityVersionResolver unityVersionResolver;

    private readonly IUnityEditorPathResolver unityEditorPathResolver;

    private readonly IProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="OpsCatalogLiveReader" /> class. </summary>
    /// <param name="unityIpcClient"> The Unity IPC client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <param name="unityEditorPathResolver"> The Unity editor path resolver dependency. </param>
    /// <param name="processRunner"> The external process runner dependency. </param>
    public OpsCatalogLiveReader (
        IUnityIpcClient unityIpcClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider,
        IUnityVersionResolver unityVersionResolver,
        IUnityEditorPathResolver unityEditorPathResolver,
        IProcessRunner processRunner)
    {
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <inheritdoc />
    public ValueTask<OpsCatalogLiveReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        UnityExecutionTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        return target switch
        {
            UnityExecutionTarget.Daemon => ReadFromDaemon(unityProject, timeout, cancellationToken),
            UnityExecutionTarget.Oneshot => ReadFromOneshot(unityProject, timeout, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported execution target."),
        };
    }

    private async ValueTask<OpsCatalogLiveReadResult> ReadFromDaemon (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionTokenResult = await daemonSessionTokenProvider.Resolve(unityProject, cancellationToken).ConfigureAwait(false);
            if (!sessionTokenResult.IsSuccess)
            {
                var message = sessionTokenResult.IsSessionNotAvailable
                    ? "Daemon session token is not available."
                    : $"Daemon session token could not be resolved. {sessionTokenResult.Error!.Message}";
                return OpsCatalogLiveReadResult.Failure(message, IpcErrorCodes.InternalError);
            }

            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: $"ops-read-{Guid.NewGuid():N}",
                SessionToken: sessionTokenResult.Token!,
                Method: IpcMethodNames.OpsRead,
                Payload: IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest()));
            var response = await unityIpcClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
            {
                if (firstError != null)
                {
                    return OpsCatalogLiveReadResult.Failure(firstError.Message, firstError.Code);
                }

                return OpsCatalogLiveReadResult.Failure(
                    $"Unity daemon ops read failed with status '{status}'.",
                    IpcErrorCodes.InternalError);
            }

            if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcOpsReadResponse payload, out var payloadError))
            {
                return OpsCatalogLiveReadResult.Failure(
                    $"Unity daemon ops read payload is invalid. {payloadError.Message}",
                    IpcErrorCodes.InternalError);
            }

            if (!IndexCatalogContractValidator.TryValidateOpsEntries(payload.Operations, "operations", out var validationError))
            {
                return OpsCatalogLiveReadResult.Failure(
                    $"Unity daemon ops read payload is invalid. {validationError}",
                    IpcErrorCodes.InternalError);
            }

            return OpsCatalogLiveReadResult.Success(payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return OpsCatalogLiveReadResult.Failure(
                $"Unity daemon ops read request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                CliErrorCodes.IpcTimeout);
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return OpsCatalogLiveReadResult.Failure(
                $"Unity daemon is not running. {exception.Message}",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
        }
        catch (Exception exception)
        {
            return OpsCatalogLiveReadResult.Failure(
                $"Failed to read ops catalog from Unity daemon. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
    }

    private async ValueTask<OpsCatalogLiveReadResult> ReadFromOneshot (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var temporaryOutputPath = Path.Combine(Path.GetTempPath(), $"ucli-ops-{Guid.NewGuid():N}.json");
        var temporaryLogPath = Path.Combine(Path.GetTempPath(), $"ucli-ops-{Guid.NewGuid():N}.log");

        try
        {
            var unityVersionResult = unityVersionResolver.Resolve(unityProject.UnityProjectRoot, preferredUnityVersion: null);
            if (!unityVersionResult.IsSuccess)
            {
                return OpsCatalogLiveReadResult.Failure(
                    unityVersionResult.Error!.Message,
                    ExecutionErrorKindCodeMapper.ToCode(unityVersionResult.Error.Kind));
            }

            var unityEditorPathResult = unityEditorPathResolver.Resolve(
                unityVersionResult.UnityVersion!,
                preferredUnityEditorPath: null);
            if (!unityEditorPathResult.IsSuccess)
            {
                return OpsCatalogLiveReadResult.Failure(
                    unityEditorPathResult.Error!.Message,
                    ExecutionErrorKindCodeMapper.ToCode(unityEditorPathResult.Error.Kind));
            }

            var arguments = new List<string>
            {
                "-batchmode",
                "-nographics",
                "-projectPath",
                unityProject.UnityProjectRoot,
                "-logFile",
                temporaryLogPath,
            };
            IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(arguments, new IpcOneshotBootstrapArguments(temporaryOutputPath));

            var processResult = await processRunner.RunAsync(
                    new ProcessRunRequest(
                        FileName: unityEditorPathResult.UnityEditorPath!,
                        Arguments: arguments,
                        TimeoutSeconds: ProcessTimeoutConverter.ConvertToSeconds(timeout)),
                    cancellationToken)
                .ConfigureAwait(false);
            switch (processResult.Status)
            {
                case ProcessRunStatus.StartFailed:
                    return OpsCatalogLiveReadResult.Failure(
                        processResult.ErrorMessage ?? "Failed to start Unity process.",
                        IpcErrorCodes.InternalError);

                case ProcessRunStatus.TimedOut:
                    return OpsCatalogLiveReadResult.Failure(
                        $"Unity ops snapshot export timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                        CliErrorCodes.IpcTimeout);

                case ProcessRunStatus.Canceled:
                    return OpsCatalogLiveReadResult.Failure(
                        processResult.ErrorMessage ?? "Unity ops snapshot export was canceled.",
                        CliErrorCodes.Canceled);

                case ProcessRunStatus.Exited:
                    if (processResult.ExitCode != 0)
                    {
                        return OpsCatalogLiveReadResult.Failure(
                            processResult.ErrorMessage ?? $"Unity process exited with code {processResult.ExitCode}.",
                            IpcErrorCodes.InternalError);
                    }

                    break;

                default:
                    return OpsCatalogLiveReadResult.Failure(
                        "Unity process execution status is unknown.",
                        IpcErrorCodes.InternalError);
            }

            if (!File.Exists(temporaryOutputPath))
            {
                return OpsCatalogLiveReadResult.Failure(
                    "Unity ops snapshot export did not produce an output file.",
                    IpcErrorCodes.InternalError);
            }

            var json = await File.ReadAllTextAsync(temporaryOutputPath, cancellationToken).ConfigureAwait(false);
            var payload = JsonSerializer.Deserialize<IpcOpsReadResponse>(json, IpcJsonSerializerOptions.Default);
            if (payload == null)
            {
                return OpsCatalogLiveReadResult.Failure(
                    "Unity ops snapshot export output is invalid.",
                    IpcErrorCodes.InternalError);
            }

            if (!IndexCatalogContractValidator.TryValidateOpsEntries(payload.Operations, "operations", out var validationError))
            {
                return OpsCatalogLiveReadResult.Failure(
                    $"Unity ops snapshot export output is invalid. {validationError}",
                    IpcErrorCodes.InternalError);
            }

            return OpsCatalogLiveReadResult.Success(payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            return OpsCatalogLiveReadResult.Failure(
                $"Unity ops snapshot export output is malformed. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
        catch (Exception exception)
        {
            return OpsCatalogLiveReadResult.Failure(
                $"Failed to read ops catalog from Unity oneshot execution. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
        finally
        {
            FileUtilities.DeleteIfExists(temporaryOutputPath);
            FileUtilities.DeleteIfExists(temporaryLogPath);
        }
    }
}