using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Implements Unity batchmode child-process launch using resolved Unity editor executable paths. </summary>
internal sealed class UnityBatchmodeProcessLauncher : IUnityDaemonProcessLauncher, IUnityBatchmodeProcessLauncher
{
    private readonly IUnityVersionResolver unityVersionResolver;

    private readonly IUnityEditorPathResolver unityEditorPathResolver;

    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IUnityUcliPluginLocator unityUcliPluginLocator;

    private readonly IUnityProjectLockFileProbe unityProjectLockFileProbe;

    /// <summary> Initializes a new instance of the <see cref="UnityBatchmodeProcessLauncher" /> class. </summary>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <param name="unityEditorPathResolver"> The Unity editor path resolver dependency. </param>
    /// <param name="endpointResolver"> The IPC endpoint resolver dependency. </param>
    /// <param name="unityUcliPluginLocator"> The Unity uCLI plugin locator dependency. </param>
    /// <param name="unityProjectLockFileProbe"> The Unity project lock-file probe dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public UnityBatchmodeProcessLauncher (
        IUnityVersionResolver unityVersionResolver,
        IUnityEditorPathResolver unityEditorPathResolver,
        IIpcEndpointResolver endpointResolver,
        IUnityUcliPluginLocator unityUcliPluginLocator,
        IUnityProjectLockFileProbe unityProjectLockFileProbe)
    {
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.unityUcliPluginLocator = unityUcliPluginLocator ?? throw new ArgumentNullException(nameof(unityUcliPluginLocator));
        this.unityProjectLockFileProbe = unityProjectLockFileProbe ?? throw new ArgumentNullException(nameof(unityProjectLockFileProbe));
    }

    /// <summary> Launches one Unity batchmode daemon process for the specified project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="unityLogPath"> The Unity log file path passed to Unity <c>-logFile</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon launch result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<UnityDaemonLaunchResult> Launch (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        string unityLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        var endpoint = endpointResolver.Resolve(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var batchmodeLaunchResult = await Launch(
                unityProject,
                new IpcDaemonBootstrapArguments(
                    RepositoryRoot: unityProject.RepositoryRoot,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    SessionPath: UcliStoragePathResolver.ResolveSessionPath(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint),
                    SessionIssuedAtUtc: session.IssuedAtUtc,
                    EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
                    EndpointAddress: endpoint.Address),
                unityLogPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!batchmodeLaunchResult.IsSuccess)
        {
            return UnityDaemonLaunchResult.Failure(batchmodeLaunchResult.Error!);
        }

        await using var processHandle = batchmodeLaunchResult.ProcessHandle!;
        return UnityDaemonLaunchResult.Success(processHandle.ProcessId);
    }

    /// <inheritdoc />
    public ValueTask<UnityBatchmodeProcessLaunchResult> Launch (
        ResolvedUnityProjectContext unityProject,
        IpcBatchmodeBootstrapArguments bootstrapArguments,
        string unityLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(bootstrapArguments);

        if (string.IsNullOrWhiteSpace(unityLogPath))
        {
            return ValueTask.FromResult(UnityBatchmodeProcessLaunchResult.Failure(ExecutionError.InvalidArgument(
                "Unity log path must not be empty.")));
        }

        return LaunchValidated(
            unityProject,
            bootstrapArguments,
            unityLogPath,
            cancellationToken);
    }

    private async ValueTask<UnityBatchmodeProcessLaunchResult> LaunchValidated (
        ResolvedUnityProjectContext unityProject,
        IpcBatchmodeBootstrapArguments bootstrapArguments,
        string unityLogPath,
        CancellationToken cancellationToken)
    {
        var projectLockValidationError = TryValidateProjectLock(unityProject.UnityProjectRoot);
        if (projectLockValidationError != null)
        {
            return UnityBatchmodeProcessLaunchResult.Failure(projectLockValidationError);
        }

        var pluginLocateResult = await unityUcliPluginLocator.Locate(
                unityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (!pluginLocateResult.IsSuccess)
        {
            return UnityBatchmodeProcessLaunchResult.Failure(pluginLocateResult.Error!);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var unityVersionResult = unityVersionResolver.Resolve(unityProject.UnityProjectRoot, preferredUnityVersion: null);
        if (!unityVersionResult.IsSuccess)
        {
            return UnityBatchmodeProcessLaunchResult.Failure(unityVersionResult.Error!);
        }

        var unityEditorPathResult = unityEditorPathResolver.Resolve(unityVersionResult.UnityVersion!, preferredUnityEditorPath: null);
        if (!unityEditorPathResult.IsSuccess)
        {
            return UnityBatchmodeProcessLaunchResult.Failure(unityEditorPathResult.Error!);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var unityLogDirectoryPath = Path.GetDirectoryName(unityLogPath);
            if (!string.IsNullOrWhiteSpace(unityLogDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(unityLogDirectoryPath);
            }

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = unityEditorPathResult.UnityEditorPath!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            var argumentTokens = BuildArgumentTokens(unityProject.UnityProjectRoot, unityLogPath, bootstrapArguments);
            for (var i = 0; i < argumentTokens.Count; i++)
            {
                processStartInfo.ArgumentList.Add(argumentTokens[i]);
            }

            // NOTE: An external Unity editor can open the project while plugin and editor paths are being resolved.
            projectLockValidationError = TryValidateProjectLock(unityProject.UnityProjectRoot);
            if (projectLockValidationError != null)
            {
                return UnityBatchmodeProcessLaunchResult.Failure(projectLockValidationError);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                return UnityBatchmodeProcessLaunchResult.Failure(ExecutionError.InternalError(
                    "Unity batchmode process could not be started."));
            }

            return UnityBatchmodeProcessLaunchResult.Success(new UnityBatchmodeProcessHandle(process));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityBatchmodeProcessLaunchResult.Failure(ExecutionError.InvalidArgument(
                $"Unity batchmode launch path is invalid. {exception.Message}"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UnityBatchmodeProcessLaunchResult.Failure(ExecutionError.InternalError(
                $"Failed to start Unity batchmode process. {exception.Message}"));
        }
    }

    private ExecutionError? TryValidateProjectLock (string unityProjectRoot)
    {
        var lockFileProbeResult = unityProjectLockFileProbe.Probe(unityProjectRoot);
        if (!lockFileProbeResult.IsSuccess)
        {
            return ExecutionError.InternalError(
                lockFileProbeResult.ErrorMessage!,
                UcliCoreErrorCodes.InternalError);
        }

        if (!lockFileProbeResult.IsLocked)
        {
            return null;
        }

        return CreateProjectAlreadyOpenError(unityProjectRoot, lockFileProbeResult.LockFilePath);
    }

    private static ExecutionError CreateProjectAlreadyOpenError (
        string unityProjectRoot,
        string? lockFilePath)
    {
        return ExecutionError.InternalError(
            UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProjectRoot, lockFilePath),
            UnityProcessErrorCodes.UnityProjectAlreadyOpen);
    }

    /// <summary> Builds Unity editor command-line argument tokens for batchmode host startup. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="unityLogPath"> The Unity log file path. </param>
    /// <param name="bootstrapArguments"> The bootstrap argument payload. </param>
    /// <returns> The ordered command-line argument token list. </returns>
    internal static IReadOnlyList<string> BuildArgumentTokens (
        string unityProjectRoot,
        string unityLogPath,
        IpcBatchmodeBootstrapArguments bootstrapArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityLogPath);
        ArgumentNullException.ThrowIfNull(bootstrapArguments);

        var tokens = new List<string>
        {
            "-batchmode",
            "-nographics",
            "-projectPath",
            unityProjectRoot,
            "-logFile",
            unityLogPath,
        };
        IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(tokens, bootstrapArguments);
        return tokens;
    }
}
