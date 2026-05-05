using MackySoft.Ucli.Features.Daemon.Supervisor.Invocation;
using MackySoft.Ucli.Infrastructure.Xml;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Creates macOS LaunchAgent plist documents for the worktree-local supervisor. </summary>
internal static class LaunchAgentPlistDocumentFactory
{
    /// <summary> Builds one LaunchAgent plist document. </summary>
    /// <param name="label"> The LaunchAgent label. </param>
    /// <param name="launchCommand"> The base command used to relaunch uCLI. </param>
    /// <param name="storageRoot"> The supervisor storage root. </param>
    /// <param name="logPath"> The shared stdout and stderr log path. </param>
    /// <returns> The complete plist XML document without a trailing newline. </returns>
    /// <exception cref="ArgumentException"> Thrown when a required string value is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="launchCommand" /> is <see langword="null" />. </exception>
    public static string Build (
        string label,
        SupervisorLaunchCommand launchCommand,
        string storageRoot,
        string logPath)
    {
        ThrowIfNullOrWhiteSpace(label, nameof(label));
        ArgumentNullException.ThrowIfNull(launchCommand);
        ThrowIfNullOrWhiteSpace(storageRoot, nameof(storageRoot));
        ThrowIfNullOrWhiteSpace(logPath, nameof(logPath));

        var programArguments = BuildProgramArguments(launchCommand, storageRoot);
        return PropertyListXmlBuilder.BuildRootDictionary(builder =>
        {
            builder.WriteString("Label", label);
            builder.WriteStringArray("ProgramArguments", programArguments);
            builder.WriteString("WorkingDirectory", storageRoot);
            builder.WriteBoolean("RunAtLoad", true);
            builder.WriteString("StandardOutPath", logPath);
            builder.WriteString("StandardErrorPath", logPath);
        });
    }

    private static string[] BuildProgramArguments (
        SupervisorLaunchCommand launchCommand,
        string storageRoot)
    {
        var supervisorArguments = SupervisorInvocationArguments.Build(storageRoot);
        var arguments = new string[1 + launchCommand.Arguments.Count + supervisorArguments.Length];
        var index = 0;

        arguments[index] = launchCommand.FileName;
        index++;
        for (var i = 0; i < launchCommand.Arguments.Count; i++)
        {
            arguments[index] = launchCommand.Arguments[i];
            index++;
        }

        for (var i = 0; i < supervisorArguments.Length; i++)
        {
            arguments[index] = supervisorArguments[i];
            index++;
        }

        return arguments;
    }

    private static void ThrowIfNullOrWhiteSpace (
        string value,
        string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", paramName);
        }
    }
}
