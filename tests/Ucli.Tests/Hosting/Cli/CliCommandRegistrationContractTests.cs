using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliCommandRegistrationContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task HelpOutput_CommandPathsMatchCommandCatalog ()
    {
        var result = await CliProcessRunner.RunCommandAsync("--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(
            UcliCommandCatalog.CommandPaths.Order(StringComparer.Ordinal).ToArray(),
            ExtractHelpCommandPaths(result.StdOut).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CommandCatalog_CommandPathsMatchPublicLeafCommands ()
    {
        var registeredPublicCommandNames = UcliCommandCatalog.CommandPaths
            .Select(static commandPath => commandPath.Replace(' ', '.'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var publicLeafCommandNames = ExtractPublicLeafCommandNames()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(publicLeafCommandNames, registeredPublicCommandNames);
    }

    private static string[] ExtractHelpCommandPaths (string helpOutput)
    {
        var commandPaths = new List<string>();
        var lines = helpOutput.Split('\n');
        var isCommandSection = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (!isCommandSection)
            {
                isCommandSection = string.Equals(line.Trim(), "Commands:", StringComparison.Ordinal);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (!line.StartsWith("  ", StringComparison.Ordinal))
            {
                break;
            }

            commandPaths.Add(ExtractCommandPath(line));
        }

        return commandPaths.ToArray();
    }

    private static string[] ExtractPublicLeafCommandNames ()
    {
        var knownCommandNames = UcliPublicCommandCatalog.KnownCommands
            .Select(static command => command.Name)
            .ToArray();
        return knownCommandNames
            .Where(commandName => !knownCommandNames.Any(otherCommandName =>
                otherCommandName.Length > commandName.Length
                && otherCommandName.StartsWith(commandName + ".", StringComparison.Ordinal)))
            .ToArray();
    }

    private static string ExtractCommandPath (string helpLine)
    {
        var trimmed = helpLine.TrimStart();
        for (var i = 0; i < trimmed.Length - 1; i++)
        {
            if (trimmed[i] == ' ' && trimmed[i + 1] == ' ')
            {
                return trimmed[..i];
            }
        }

        throw new InvalidOperationException($"Command help line does not contain a description separator: {helpLine}");
    }

    private static string FindRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ucli.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test base directory.");
    }
}
