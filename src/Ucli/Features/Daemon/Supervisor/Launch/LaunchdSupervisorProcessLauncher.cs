using System.Text;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Launches the worktree-local supervisor through macOS LaunchAgent ownership. </summary>
internal sealed class LaunchdSupervisorProcessLauncher
{
    private readonly SupervisorExternalProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="LaunchdSupervisorProcessLauncher" /> class. </summary>
    /// <param name="processRunner"> The external process-runner dependency. </param>
    public LaunchdSupervisorProcessLauncher (SupervisorExternalProcessRunner processRunner)
    {
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <summary> Launches the supervisor for the specified storage root by using <c>launchctl</c>. </summary>
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
            var plistPath = UcliStoragePathResolver.ResolveSupervisorLaunchAgentPlistPath(normalizedStorageRoot);
            var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(normalizedStorageRoot);
            var label = BuildLaunchdLabel(normalizedStorageRoot);
            var userId = await ResolveCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
            if (userId == null)
            {
                return ExecutionError.InternalError("Current user identifier could not be resolved for supervisor LaunchAgent.");
            }

            var userDomain = $"gui/{userId}";
            var plistDirectoryPath = Path.GetDirectoryName(plistPath);
            if (!string.IsNullOrWhiteSpace(plistDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(plistDirectoryPath);
            }

            var plistContents = LaunchAgentPlistDocumentFactory.Build(label, launchCommand, normalizedStorageRoot, logPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(plistPath, plistContents + Environment.NewLine, cancellationToken).ConfigureAwait(false);

            await processRunner.RunIgnoringExitCodeAsync(
                    "launchctl",
                    ["bootout", $"{userDomain}/{label}"],
                    cancellationToken)
                .ConfigureAwait(false);

            var bootstrapResult = await processRunner.RunAsync(
                    "launchctl",
                    ["bootstrap", userDomain, plistPath],
                    cancellationToken)
                .ConfigureAwait(false);
            if (bootstrapResult.ExitCode != 0)
            {
                return ExecutionError.InternalError(
                    $"Failed to bootstrap supervisor LaunchAgent. {SupervisorExternalProcessRunner.FormatFailure(bootstrapResult)}");
            }

            var kickstartResult = await processRunner.RunAsync(
                    "launchctl",
                    ["kickstart", "-k", $"{userDomain}/{label}"],
                    cancellationToken)
                .ConfigureAwait(false);
            if (kickstartResult.ExitCode != 0)
            {
                return ExecutionError.InternalError(
                    $"Failed to start supervisor LaunchAgent. {SupervisorExternalProcessRunner.FormatFailure(kickstartResult)}");
            }

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ExecutionError.InternalError($"Failed to launch supervisor with launchctl. {exception.Message}");
        }
    }

    private async ValueTask<string?> ResolveCurrentUserIdAsync (CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync("id", ["-u"], cancellationToken).ConfigureAwait(false);
        var output = result.StandardOutput.Trim();
        return result.ExitCode == 0 && output.Length > 0 ? output : null;
    }

    private static string BuildLaunchdLabel (string normalizedStorageRoot)
    {
        return "dev.mackysoft.ucli.supervisor." + BuildIdentityHash(normalizedStorageRoot)[..16];
    }

    private static string BuildIdentityHash (string normalizedStorageRoot)
    {
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(normalizedStorageRoot));
    }
}
