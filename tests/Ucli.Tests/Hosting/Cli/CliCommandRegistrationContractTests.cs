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
    public void CommandReference_CommandPathsMatchCommandCatalog ()
    {
        var referencePath = Path.Combine(FindRepositoryRoot(), "docs", "uCLI-command-reference.md");
        var referenceText = File.ReadAllText(referencePath);

        Assert.Equal(
            UcliCommandCatalog.CommandPaths.Order(StringComparer.Ordinal).ToArray(),
            ExtractCommandReferenceCommandPaths(referenceText).Order(StringComparer.Ordinal).ToArray());
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

    private static string[] ExtractCommandReferenceCommandPaths (string referenceText)
    {
        var commandPaths = new List<string>();
        var lines = referenceText.Split('\n');
        var isCommandPathSection = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmedLine = line.Trim();
            if (!isCommandPathSection)
            {
                isCommandPathSection = string.Equals(trimmedLine, "### 実行可能 command paths", StringComparison.Ordinal);
                continue;
            }

            if (trimmedLine.StartsWith("### ", StringComparison.Ordinal)
                || trimmedLine.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            const string commandPrefix = "| `ucli ";
            if (!trimmedLine.StartsWith(commandPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var commandEnd = trimmedLine.IndexOf('`', commandPrefix.Length);
            if (commandEnd < 0)
            {
                throw new InvalidOperationException($"Command reference row does not close the command literal: {line}");
            }

            commandPaths.Add(trimmedLine[commandPrefix.Length..commandEnd]);
        }

        if (!isCommandPathSection)
        {
            throw new InvalidOperationException("Command reference does not contain '### 実行可能 command paths'.");
        }

        if (commandPaths.Count == 0)
        {
            throw new InvalidOperationException("Command reference command path section is empty.");
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
