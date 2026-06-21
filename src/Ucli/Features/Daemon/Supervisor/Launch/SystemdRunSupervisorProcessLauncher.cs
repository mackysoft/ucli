using System.Text;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Launches the worktree-local supervisor through <c>systemd-run --user</c>. </summary>
internal sealed class SystemdRunSupervisorProcessLauncher
{
    private readonly SupervisorExternalProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="SystemdRunSupervisorProcessLauncher" /> class. </summary>
    /// <param name="processRunner"> The external process-runner dependency. </param>
    public SystemdRunSupervisorProcessLauncher (SupervisorExternalProcessRunner processRunner)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <summary> Launches the supervisor for the specified storage root by using <c>systemd-run</c>. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="launchCommand"> The resolved relaunch command. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> One structured error when launch fails; otherwise <see langword="null" />. </returns>
    public async ValueTask<ExecutionError?> LaunchAsync (
        string storageRoot,
        SupervisorLaunchCommand launchCommand,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(launchCommand);

        try
        {
            var normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);
            var unitName = BuildSystemdUnitName(normalizedStorageRoot);
            var arguments = BuildArguments(
                normalizedStorageRoot,
                unitName,
                launchCommand,
                SupervisorRuntimeTraceEnvironment.Capture());

            var launchResult = await processRunner.RunAsync(
                    "systemd-run",
                    arguments,
                    cancellationToken)
                .ConfigureAwait(false);
            if (launchResult.ExitCode == 0)
            {
                return null;
            }

            return ExecutionError.InternalError(
                $"Failed to launch supervisor with systemd-run. {SupervisorExternalProcessRunner.FormatFailure(launchResult)}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ExecutionError.InternalError($"Failed to launch supervisor with systemd-run. {exception.Message}");
        }
    }

    private static string BuildSystemdUnitName (string normalizedStorageRoot)
    {
        return "mackysoft-ucli-supervisor-" + BuildIdentityHash(normalizedStorageRoot)[..16];
    }

    internal static IReadOnlyList<string> BuildArguments (
        string normalizedStorageRoot,
        string unitName,
        SupervisorLaunchCommand launchCommand)
    {
        return BuildArguments(
            normalizedStorageRoot,
            unitName,
            launchCommand,
            Array.Empty<KeyValuePair<string, string>>());
    }

    internal static IReadOnlyList<string> BuildArguments (
        string normalizedStorageRoot,
        string unitName,
        SupervisorLaunchCommand launchCommand,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(launchCommand);
        ArgumentNullException.ThrowIfNull(environmentVariables);

        var arguments = new List<string>
        {
            "--user",
            "--quiet",
            "--collect",
            "--unit",
            unitName,
            "--working-directory",
            normalizedStorageRoot,
        };
        for (var i = 0; i < environmentVariables.Count; i++)
        {
            var environmentVariable = environmentVariables[i];
            if (string.IsNullOrWhiteSpace(environmentVariable.Key))
            {
                throw new ArgumentException("Environment variable name must not be empty.", nameof(environmentVariables));
            }

            if (environmentVariable.Value == null)
            {
                throw new ArgumentNullException(nameof(environmentVariables), "Environment variable values must not contain null.");
            }

            arguments.Add("--setenv");
            arguments.Add($"{environmentVariable.Key}={environmentVariable.Value}");
        }

        arguments.Add(launchCommand.FileName);
        arguments.AddRange(launchCommand.Arguments);
        arguments.AddRange(SupervisorInvocationArguments.Build(normalizedStorageRoot));
        return arguments;
    }

    private static string BuildIdentityHash (string normalizedStorageRoot)
    {
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(normalizedStorageRoot));
    }
}
