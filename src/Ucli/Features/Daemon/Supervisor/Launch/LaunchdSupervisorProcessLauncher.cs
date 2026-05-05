using System.Security;
using System.Text;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Cryptography;
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
    public async ValueTask<ExecutionError?> Launch (
        string storageRoot,
        SupervisorLaunchCommand launchCommand,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(launchCommand);

        try
        {
            var normalizedStorageRoot = Path.GetFullPath(storageRoot);
            var plistPath = UcliStoragePathResolver.ResolveSupervisorLaunchAgentPlistPath(normalizedStorageRoot);
            var logPath = UcliStoragePathResolver.ResolveSupervisorLogPath(normalizedStorageRoot);
            var label = BuildLaunchdLabel(normalizedStorageRoot);
            var userId = await ResolveCurrentUserId(cancellationToken).ConfigureAwait(false);
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

            var plistContents = BuildLaunchAgentPlist(label, launchCommand, normalizedStorageRoot, logPath);
            await FileUtilities.WriteAllTextAtomically(plistPath, plistContents + Environment.NewLine, cancellationToken).ConfigureAwait(false);

            await processRunner.RunIgnoringExitCode(
                    "launchctl",
                    ["bootout", $"{userDomain}/{label}"],
                    cancellationToken)
                .ConfigureAwait(false);

            var bootstrapResult = await processRunner.Run(
                    "launchctl",
                    ["bootstrap", userDomain, plistPath],
                    cancellationToken)
                .ConfigureAwait(false);
            if (bootstrapResult.ExitCode != 0)
            {
                return ExecutionError.InternalError(
                    $"Failed to bootstrap supervisor LaunchAgent. {SupervisorExternalProcessRunner.FormatFailure(bootstrapResult)}");
            }

            var kickstartResult = await processRunner.Run(
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

    private async ValueTask<string?> ResolveCurrentUserId (CancellationToken cancellationToken)
    {
        var result = await processRunner.Run("id", ["-u"], cancellationToken).ConfigureAwait(false);
        var output = result.StandardOutput.Trim();
        return result.ExitCode == 0 && output.Length > 0 ? output : null;
    }

    private static string BuildLaunchAgentPlist (
        string label,
        SupervisorLaunchCommand launchCommand,
        string storageRoot,
        string logPath)
    {
        var escapedLabel = EscapeXml(label);
        var escapedStorageRoot = EscapeXml(storageRoot);
        var escapedLogPath = EscapeXml(logPath);

        var builder = new StringBuilder();
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
        builder.AppendLine("<plist version=\"1.0\">");
        builder.AppendLine("<dict>");
        builder.AppendLine("  <key>Label</key>");
        builder.AppendLine($"  <string>{escapedLabel}</string>");
        builder.AppendLine("  <key>ProgramArguments</key>");
        builder.AppendLine("  <array>");
        builder.AppendLine($"    <string>{EscapeXml(launchCommand.FileName)}</string>");
        for (var i = 0; i < launchCommand.Arguments.Count; i++)
        {
            builder.AppendLine($"    <string>{EscapeXml(launchCommand.Arguments[i])}</string>");
        }

        var supervisorArguments = SupervisorInvocationArguments.Build(storageRoot);
        for (var i = 0; i < supervisorArguments.Length; i++)
        {
            builder.AppendLine($"    <string>{EscapeXml(supervisorArguments[i])}</string>");
        }

        builder.AppendLine("  </array>");
        builder.AppendLine("  <key>WorkingDirectory</key>");
        builder.AppendLine($"  <string>{escapedStorageRoot}</string>");
        builder.AppendLine("  <key>RunAtLoad</key>");
        builder.AppendLine("  <true/>");
        builder.AppendLine("  <key>StandardOutPath</key>");
        builder.AppendLine($"  <string>{escapedLogPath}</string>");
        builder.AppendLine("  <key>StandardErrorPath</key>");
        builder.AppendLine($"  <string>{escapedLogPath}</string>");
        builder.AppendLine("</dict>");
        builder.AppendLine("</plist>");
        return builder.ToString();
    }

    private static string BuildLaunchdLabel (string normalizedStorageRoot)
    {
        return "dev.mackysoft.ucli.supervisor." + BuildIdentityHash(normalizedStorageRoot)[..16];
    }

    private static string BuildIdentityHash (string normalizedStorageRoot)
    {
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(normalizedStorageRoot));
    }

    private static string EscapeXml (string value)
    {
        return SecurityElement.Escape(value)
            ?? throw new InvalidOperationException("XML escaping failed.");
    }
}
