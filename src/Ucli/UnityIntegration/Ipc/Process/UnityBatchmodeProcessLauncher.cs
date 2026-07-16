using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Implements Unity batchmode child-process launch using resolved Unity editor executable paths. </summary>
internal sealed class UnityBatchmodeProcessLauncher : IUnityDaemonProcessLauncher, IUnityBatchmodeProcessLauncher
{
    private const int RedirectedOutputDrainBufferLength = 4096;

    private readonly IUnityVersionResolver unityVersionResolver;

    private readonly IUnityEditorPathResolver unityEditorPathResolver;

    private readonly IUnityUcliPluginLocator unityUcliPluginLocator;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    private readonly UnityBatchmodeProcessLifetimeOwner processLifetimeOwner = new();

    /// <summary> Initializes a new instance of the <see cref="UnityBatchmodeProcessLauncher" /> class. </summary>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <param name="unityEditorPathResolver"> The Unity editor path resolver dependency. </param>
    /// <param name="unityUcliPluginLocator"> The Unity uCLI plugin locator dependency. </param>
    /// <param name="unityProjectLockPreflightService"> The Unity project lock preflight service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public UnityBatchmodeProcessLauncher (
        IUnityVersionResolver unityVersionResolver,
        IUnityEditorPathResolver unityEditorPathResolver,
        IUnityUcliPluginLocator unityUcliPluginLocator,
        IUnityProjectLockPreflightService unityProjectLockPreflightService)
    {
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
        this.unityUcliPluginLocator = unityUcliPluginLocator ?? throw new ArgumentNullException(nameof(unityUcliPluginLocator));
        this.unityProjectLockPreflightService = unityProjectLockPreflightService ?? throw new ArgumentNullException(nameof(unityProjectLockPreflightService));
    }

    /// <summary> Launches one Unity batchmode daemon process for the specified project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="unityLogPath"> The Unity log file path passed to Unity <c>-logFile</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon launch result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<UnityDaemonLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        string unityLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var batchmodeLaunchResult = await LaunchBatchmodeAsync(
                unityProject,
                new IpcDaemonBootstrapArguments(
                    RepositoryRoot: unityProject.RepositoryRoot,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    SessionPath: UcliStoragePathResolver.ResolveSessionPath(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint),
                    SessionGenerationId: session.SessionGenerationId,
                    SessionIssuedAtUtc: session.IssuedAtUtc,
                    Endpoint: endpoint),
                unityLogPath,
                UnityBatchmodeLaunchOptions.Default,
                cancellationToken)
            .ConfigureAwait(false);
        if (!batchmodeLaunchResult.IsSuccess)
        {
            return UnityDaemonLaunchResult.Failure(batchmodeLaunchResult.Error!);
        }

        var processHandle = batchmodeLaunchResult.ProcessHandle!;
        var launchResult = await UnityProcessOwnership.ResolveDaemonLaunchAsync(
                processHandle,
                cancellationToken)
            .ConfigureAwait(false);
        if (!launchResult.IsSuccess)
        {
            return launchResult;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            processLifetimeOwner.Transfer(processHandle);
            return launchResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await UnityProcessOwnership.TerminateAndDisposeBestEffortAsync(processHandle).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await UnityProcessOwnership.TerminateAndDisposeBestEffortAsync(processHandle).ConfigureAwait(false);
            return UnityDaemonLaunchResult.Failure(ExecutionError.InternalError(
                $"Failed to transfer Unity batchmode process lifetime ownership. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<UnityBatchmodeProcessLaunchResult> LaunchOneshotAsync (
        ResolvedUnityProjectContext unityProject,
        IpcOneshotBootstrapEnvelope bootstrapEnvelope,
        string unityLogPath,
        UnityBatchmodeLaunchOptions launchOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(bootstrapEnvelope);
        ArgumentNullException.ThrowIfNull(launchOptions);

        if (string.IsNullOrWhiteSpace(unityLogPath))
        {
            return UnityBatchmodeProcessLaunchResult.Failure(ExecutionError.InvalidArgument(
                "Unity log path must not be empty."));
        }

        var envelopeCreated = false;
        try
        {
            OneshotBootstrapEnvelopeStore.Create(unityProject.RepositoryRoot, bootstrapEnvelope);
            envelopeCreated = true;

            var launchResult = await LaunchBatchmodeAsync(
                    unityProject,
                    new IpcOneshotBootstrapArguments(bootstrapEnvelope.BootstrapId),
                    unityLogPath,
                    launchOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(unityProject.RepositoryRoot, bootstrapEnvelope);
                return launchResult;
            }

            return UnityBatchmodeProcessLaunchResult.Success(
                new OneshotBootstrapOwnedProcessHandle(
                    launchResult.ProcessHandle!,
                    unityProject.RepositoryRoot,
                    bootstrapEnvelope));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (envelopeCreated)
            {
                OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(unityProject.RepositoryRoot, bootstrapEnvelope);
            }

            throw;
        }
        catch (Exception exception)
        {
            if (envelopeCreated)
            {
                OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(unityProject.RepositoryRoot, bootstrapEnvelope);
            }

            return UnityBatchmodeProcessLaunchResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare Unity oneshot bootstrap. {exception.Message}"));
        }
    }

    private async ValueTask<UnityBatchmodeProcessLaunchResult> LaunchBatchmodeAsync (
        ResolvedUnityProjectContext unityProject,
        IpcBatchmodeBootstrapArguments bootstrapArguments,
        string unityLogPath,
        UnityBatchmodeLaunchOptions launchOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);

        var projectLockValidationError = await ValidateProjectLockAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (projectLockValidationError != null)
        {
            return UnityBatchmodeProcessLaunchResult.Failure(projectLockValidationError);
        }

        var pluginLocateResult = await unityUcliPluginLocator.LocateAsync(
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
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var argumentTokens = BuildArgumentTokens(unityProject.UnityProjectRoot, unityLogPath, bootstrapArguments, launchOptions);
            for (var i = 0; i < argumentTokens.Count; i++)
            {
                processStartInfo.ArgumentList.Add(argumentTokens[i]);
            }

            // NOTE: An external Unity editor can open the project while plugin and editor paths are being resolved.
            projectLockValidationError = await ValidateProjectLockAsync(unityProject, cancellationToken).ConfigureAwait(false);
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

            var processHandle = new UnityProcessHandle(process);
            try
            {
                StartRedirectedOutputDrain(process);
                cancellationToken.ThrowIfCancellationRequested();
                return UnityBatchmodeProcessLaunchResult.Success(processHandle);
            }
            catch (Exception)
            {
                await UnityProcessOwnership.TerminateAndDisposeBestEffortAsync(processHandle).ConfigureAwait(false);
                throw;
            }
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

    private static void StartRedirectedOutputDrain (System.Diagnostics.Process process)
    {
        _ = DrainRedirectedOutputAsync(process.StandardOutput);
        _ = DrainRedirectedOutputAsync(process.StandardError);
    }

    private static async Task DrainRedirectedOutputAsync (TextReader reader)
    {
        var buffer = new char[RedirectedOutputDrainBufferLength];
        try
        {
            while (await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or IOException or InvalidOperationException)
        {
            // NOTE: The process handle owns these redirected streams. Disposing or killing
            // the process can close them while background drains are still reading.
        }
    }

    private async ValueTask<ExecutionError?> ValidateProjectLockAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var preflightResult = await unityProjectLockPreflightService.PrepareForUnityProcessStartAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        return UnityProjectLockPreflightErrorFactory.CreateLaunchBlockingError(unityProject, preflightResult);
    }

    /// <summary> Builds Unity editor command-line argument tokens for batchmode host startup. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="unityLogPath"> The Unity log file path. </param>
    /// <param name="bootstrapArguments"> The bootstrap argument payload. </param>
    /// <returns> The ordered command-line argument token list. </returns>
    internal static IReadOnlyList<string> BuildArgumentTokens (
        string unityProjectRoot,
        string unityLogPath,
        IpcBatchmodeBootstrapArguments bootstrapArguments,
        UnityBatchmodeLaunchOptions? launchOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityLogPath);
        ArgumentNullException.ThrowIfNull(bootstrapArguments);
        launchOptions ??= UnityBatchmodeLaunchOptions.Default;

        var tokens = new List<string>
        {
            "-batchmode",
            "-nographics",
            "-projectPath",
            unityProjectRoot,
            "-logFile",
            unityLogPath,
        };
        if (launchOptions.ActiveBuildProfilePath != null)
        {
            tokens.Add("-activeBuildProfile");
            tokens.Add(launchOptions.ActiveBuildProfilePath.Value);
        }

        IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(tokens, bootstrapArguments);
        if (bootstrapArguments is IpcOneshotBootstrapArguments)
        {
            tokens.Add("-executeMethod");
            tokens.Add("MackySoft.Ucli.Unity.Editor.BuildExecuteMethodBridge.Run");
        }

        return tokens;
    }
}
