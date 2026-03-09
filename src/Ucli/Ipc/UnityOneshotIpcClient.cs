using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject.Resolution;

namespace MackySoft.Ucli.Ipc;

/// <summary> Executes one IPC request through Unity oneshot batchmode startup and file-based response handoff. </summary>
internal sealed class UnityOneshotIpcClient : IUnityOneshotIpcClient
{
    private readonly IUnityVersionResolver unityVersionResolver;

    private readonly IUnityEditorPathResolver unityEditorPathResolver;

    private readonly IProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="UnityOneshotIpcClient" /> class. </summary>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <param name="unityEditorPathResolver"> The Unity editor path resolver dependency. </param>
    /// <param name="processRunner"> The external process runner dependency. </param>
    public UnityOneshotIpcClient (
        IUnityVersionResolver unityVersionResolver,
        IUnityEditorPathResolver unityEditorPathResolver,
        IProcessRunner processRunner)
    {
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <inheritdoc />
    public async ValueTask<UnityIpcRequestExecutionResult> SendAsync (
        string unityProjectRoot,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var temporaryRequestPath = Path.Combine(Path.GetTempPath(), $"ucli-oneshot-request-{Guid.NewGuid():N}.json");
        var temporaryResponsePath = Path.Combine(Path.GetTempPath(), $"ucli-oneshot-response-{Guid.NewGuid():N}.json");
        var temporaryLogPath = Path.Combine(Path.GetTempPath(), $"ucli-oneshot-{Guid.NewGuid():N}.log");

        try
        {
            var unityVersionResult = unityVersionResolver.Resolve(unityProjectRoot, preferredUnityVersion: null);
            if (!unityVersionResult.IsSuccess)
            {
                return UnityIpcRequestExecutionResult.Failure(
                    unityVersionResult.Error!.Message,
                    ExecutionErrorKindCodeMapper.ToCode(unityVersionResult.Error.Kind));
            }

            var unityEditorPathResult = unityEditorPathResolver.Resolve(
                unityVersionResult.UnityVersion!,
                preferredUnityEditorPath: null);
            if (!unityEditorPathResult.IsSuccess)
            {
                return UnityIpcRequestExecutionResult.Failure(
                    unityEditorPathResult.Error!.Message,
                    ExecutionErrorKindCodeMapper.ToCode(unityEditorPathResult.Error.Kind));
            }

            var arguments = new List<string>
            {
                "-batchmode",
                "-nographics",
                "-projectPath",
                unityProjectRoot,
                "-logFile",
                temporaryLogPath,
            };
            await WriteRequest(
                    temporaryRequestPath,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);
            IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(
                arguments,
                new IpcOneshotBootstrapArguments(
                    temporaryRequestPath,
                    temporaryResponsePath));

            var processResult = await processRunner.RunAsync(
                    new ProcessRunRequest(
                        FileName: unityEditorPathResult.UnityEditorPath!,
                        Arguments: arguments,
                        Timeout: timeout),
                    cancellationToken)
                .ConfigureAwait(false);
            switch (processResult.Status)
            {
                case ProcessRunStatus.StartFailed:
                    return UnityIpcRequestExecutionResult.Failure(
                        processResult.ErrorMessage ?? "Failed to start Unity process.",
                        IpcErrorCodes.InternalError);

                case ProcessRunStatus.TimedOut:
                    return UnityIpcRequestExecutionResult.Failure(
                        $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                        CliErrorCodes.IpcTimeout);

                case ProcessRunStatus.Canceled:
                    return UnityIpcRequestExecutionResult.Failure(
                        processResult.ErrorMessage ?? "Unity oneshot IPC request was canceled.",
                        CliErrorCodes.Canceled);

                case ProcessRunStatus.Exited:
                    if (processResult.ExitCode != 0)
                    {
                        return UnityIpcRequestExecutionResult.Failure(
                            processResult.ErrorMessage ?? $"Unity process exited with code {processResult.ExitCode}.",
                            IpcErrorCodes.InternalError);
                    }

                    break;

                default:
                    return UnityIpcRequestExecutionResult.Failure(
                        "Unity process execution status is unknown.",
                        IpcErrorCodes.InternalError);
            }

            if (!File.Exists(temporaryResponsePath))
            {
                return UnityIpcRequestExecutionResult.Failure(
                    "Unity oneshot IPC request did not produce a response file.",
                    IpcErrorCodes.InternalError);
            }

            var json = await File.ReadAllTextAsync(temporaryResponsePath, cancellationToken).ConfigureAwait(false);
            var response = JsonSerializer.Deserialize<IpcResponse>(json, IpcJsonSerializerOptions.Default);
            if (response == null)
            {
                return UnityIpcRequestExecutionResult.Failure(
                    "Unity oneshot IPC response is invalid.",
                    IpcErrorCodes.InternalError);
            }

            return UnityIpcRequestExecutionResult.Success(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (JsonException exception)
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Unity oneshot IPC response is malformed. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
        catch (Exception exception)
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Failed to execute Unity oneshot IPC request. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
        finally
        {
            FileUtilities.DeleteIfExists(temporaryRequestPath);
            FileUtilities.DeleteIfExists(temporaryResponsePath);
            FileUtilities.DeleteIfExists(temporaryLogPath);
        }
    }

    private static async ValueTask WriteRequest (
        string requestPath,
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPath);
        ArgumentNullException.ThrowIfNull(request);

        var directoryPath = Path.GetDirectoryName(requestPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(request, IpcJsonSerializerOptions.Default);
        await File.WriteAllTextAsync(
                requestPath,
                json + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
