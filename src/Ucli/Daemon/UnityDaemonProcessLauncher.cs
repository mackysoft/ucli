using System.Diagnostics;
using System.Text;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements Unity batchmode daemon process launch using resolved Unity editor executable paths. </summary>
internal sealed class UnityDaemonProcessLauncher : IUnityDaemonProcessLauncher
{
    private readonly IUnityVersionResolver unityVersionResolver;

    private readonly IUnityEditorPathResolver unityEditorPathResolver;

    private readonly IIpcEndpointResolver endpointResolver;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonProcessLauncher" /> class. </summary>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <param name="unityEditorPathResolver"> The Unity editor path resolver dependency. </param>
    /// <param name="endpointResolver"> The IPC endpoint resolver dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public UnityDaemonProcessLauncher (
        IUnityVersionResolver unityVersionResolver,
        IUnityEditorPathResolver unityEditorPathResolver,
        IIpcEndpointResolver endpointResolver)
    {
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
    }

    /// <summary> Launches one Unity batchmode daemon process for the specified project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="daemonLogPath"> The daemon log file path passed to Unity <c>-logFile</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon launch result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public ValueTask<UnityDaemonLaunchResult> Launch (
        ResolvedUnityProjectContext unityProject,
        string daemonLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        if (string.IsNullOrWhiteSpace(daemonLogPath))
        {
            return ValueTask.FromResult(UnityDaemonLaunchResult.Failure(ExecutionError.InvalidArgument(
                "Daemon log path must not be empty.")));
        }

        var unityVersionResult = unityVersionResolver.Resolve(unityProject.UnityProjectRoot, preferredUnityVersion: null);
        if (!unityVersionResult.IsSuccess)
        {
            return ValueTask.FromResult(UnityDaemonLaunchResult.Failure(unityVersionResult.Error!));
        }

        var unityEditorPathResult = unityEditorPathResolver.Resolve(unityVersionResult.UnityVersion!, preferredUnityEditorPath: null);
        if (!unityEditorPathResult.IsSuccess)
        {
            return ValueTask.FromResult(UnityDaemonLaunchResult.Failure(unityEditorPathResult.Error!));
        }

        try
        {
            var daemonDirectoryPath = Path.GetDirectoryName(daemonLogPath);
            if (!string.IsNullOrWhiteSpace(daemonDirectoryPath))
            {
                Directory.CreateDirectory(daemonDirectoryPath);
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = unityEditorPathResult.UnityEditorPath!,
                Arguments = BuildArguments(unityProject, daemonLogPath, endpointResolver.Resolve(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };

            var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return ValueTask.FromResult(UnityDaemonLaunchResult.Failure(ExecutionError.InternalError(
                    "Unity daemon process could not be started.")));
            }

            try
            {
                return ValueTask.FromResult(UnityDaemonLaunchResult.Success(process.Id));
            }
            finally
            {
                process.Dispose();
            }
        }
        catch (Exception exception) when (PathFormatExceptionHelper.IsPathFormatException(exception))
        {
            return ValueTask.FromResult(UnityDaemonLaunchResult.Failure(ExecutionError.InvalidArgument(
                $"Unity daemon launch path is invalid. {exception.Message}")));
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(UnityDaemonLaunchResult.Failure(ExecutionError.InternalError(
                $"Failed to start Unity daemon process. {exception.Message}")));
        }
    }

    /// <summary> Builds Unity editor command-line arguments for batchmode daemon startup. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="daemonLogPath"> The daemon log file path. </param>
    /// <returns> The command-line arguments string. </returns>
    private static string BuildArguments (
        ResolvedUnityProjectContext unityProject,
        string daemonLogPath,
        IpcEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(endpoint);

        var builder = new StringBuilder();
        AppendArgument(builder, "-batchmode");
        AppendArgument(builder, "-nographics");
        AppendArgument(builder, "-projectPath");
        AppendArgument(builder, unityProject.UnityProjectRoot);
        AppendArgument(builder, "-logFile");
        AppendArgument(builder, daemonLogPath);
        AppendArgument(builder, "-executeMethod");
        AppendArgument(builder, "MackySoft.Ucli.Unity.Ipc.UnityDaemonBootstrap.Start");
        AppendArgument(builder, "-ucliRepositoryRoot");
        AppendArgument(builder, unityProject.RepositoryRoot);
        AppendArgument(builder, "-ucliProjectFingerprint");
        AppendArgument(builder, unityProject.ProjectFingerprint);
        AppendArgument(builder, "-ucliSessionPath");
        AppendArgument(builder, DaemonStoragePathResolver.ResolveSessionPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint));
        AppendArgument(builder, "-ucliEndpointTransportKind");
        AppendArgument(builder, DaemonSessionTransportKindCodec.ToValue(endpoint.TransportKind));
        AppendArgument(builder, "-ucliEndpointAddress");
        AppendArgument(builder, endpoint.Address);
        return builder.ToString();
    }

    /// <summary> Appends one shell-safe argument token to command-line builder. </summary>
    /// <param name="builder"> The string builder instance. </param>
    /// <param name="argument"> The argument token. </param>
    private static void AppendArgument (
        StringBuilder builder,
        string argument)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        if (argument.IndexOfAny([' ', '\t', '"']) < 0)
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        builder.Append(argument.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal));
        builder.Append('"');
    }
}
