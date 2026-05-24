using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Process;

/// <summary> Implements Unity GUI Editor process launch configured for uCLI daemon session registration. </summary>
internal sealed class UnityGuiEditorProcessLauncher : IUnityGuiEditorProcessLauncher
{
    private readonly IUnityVersionResolver unityVersionResolver;

    private readonly IUnityEditorPathResolver unityEditorPathResolver;

    private readonly IUnityUcliPluginLocator unityUcliPluginLocator;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    /// <summary> Initializes a new instance of the <see cref="UnityGuiEditorProcessLauncher" /> class. </summary>
    public UnityGuiEditorProcessLauncher (
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

    /// <inheritdoc />
    public async ValueTask<UnityDaemonLaunchResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        string unityLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        if (string.IsNullOrWhiteSpace(unityLogPath))
        {
            return UnityDaemonLaunchResult.Failure(ExecutionError.InvalidArgument(
                "Unity log path must not be empty."));
        }

        var projectLockValidationError = await ValidateProjectLockAsync(unityProject, cancellationToken).ConfigureAwait(false);
        if (projectLockValidationError != null)
        {
            return UnityDaemonLaunchResult.Failure(projectLockValidationError);
        }

        var pluginLocateResult = await unityUcliPluginLocator.LocateAsync(
                unityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (!pluginLocateResult.IsSuccess)
        {
            return UnityDaemonLaunchResult.Failure(pluginLocateResult.Error!);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var unityVersionResult = unityVersionResolver.Resolve(unityProject.UnityProjectRoot, preferredUnityVersion: null);
        if (!unityVersionResult.IsSuccess)
        {
            return UnityDaemonLaunchResult.Failure(unityVersionResult.Error!);
        }

        var unityEditorPathResult = unityEditorPathResolver.Resolve(unityVersionResult.UnityVersion!, preferredUnityEditorPath: null);
        if (!unityEditorPathResult.IsSuccess)
        {
            return UnityDaemonLaunchResult.Failure(unityEditorPathResult.Error!);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var unityLogDirectoryPath = Path.GetDirectoryName(unityLogPath);
            if (!string.IsNullOrWhiteSpace(unityLogDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(unityLogDirectoryPath);
            }

            // NOTE: GUI startup diagnosis tails this uCLI-managed log before the endpoint exists.
            // Start from an empty file so stale compiler/package/dialog lines from a previous run
            // cannot be classified as blockers for this launch attempt.
            FileUtilities.DeleteIfExists(unityLogPath);

            var processStartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = unityEditorPathResult.UnityEditorPath!,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            var argumentTokens = BuildArgumentTokens(
                unityProject.UnityProjectRoot,
                unityLogPath,
                new IpcGuiBootstrapArguments(
                    OwnerProcessId: Environment.ProcessId,
                    CanShutdownProcess: true));
            for (var i = 0; i < argumentTokens.Count; i++)
            {
                processStartInfo.ArgumentList.Add(argumentTokens[i]);
            }

            // NOTE: An external Unity editor can open the project while plugin and editor paths are being resolved.
            projectLockValidationError = await ValidateProjectLockAsync(unityProject, cancellationToken).ConfigureAwait(false);
            if (projectLockValidationError != null)
            {
                return UnityDaemonLaunchResult.Failure(projectLockValidationError);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var process = System.Diagnostics.Process.Start(processStartInfo);
            if (process == null)
            {
                return UnityDaemonLaunchResult.Failure(ExecutionError.InternalError(
                    "Unity GUI Editor process could not be started."));
            }

            using (process)
            {
                return UnityDaemonLaunchResult.Success(process.Id, process.StartTime.ToUniversalTime());
            }
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityDaemonLaunchResult.Failure(ExecutionError.InvalidArgument(
                $"Unity GUI Editor launch path is invalid. {exception.Message}"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return UnityDaemonLaunchResult.Failure(ExecutionError.InternalError(
                $"Failed to start Unity GUI Editor process. {exception.Message}"));
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

    /// <summary> Builds Unity editor command-line argument tokens for GUI daemon bootstrap. </summary>
    internal static IReadOnlyList<string> BuildArgumentTokens (
        string unityProjectRoot,
        string unityLogPath,
        IpcGuiBootstrapArguments bootstrapArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityLogPath);
        ArgumentNullException.ThrowIfNull(bootstrapArguments);

        var tokens = new List<string>
        {
            "-projectPath",
            unityProjectRoot,
            "-logFile",
            unityLogPath,
        };
        IpcGuiBootstrapArgumentsCodec.AppendTokens(tokens, bootstrapArguments);
        return tokens;
    }
}
