using System.Text;
using System.Text.RegularExpressions;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliCommandRegistrationContractTests
{
    private static readonly Regex KebabCaseLongOptionPattern = new(
        "--[a-z][A-Za-z0-9]*-[A-Za-z0-9-]*",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task HelpOutput_CommandPathsMatchCommandCatalog ()
    {
        await using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        var result = await ConsoleAppHelpRunner.RunRootHelpAsync(serviceProvider);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Equal(
            UcliCommandCatalog.CommandPaths.Order(StringComparer.Ordinal).ToArray(),
            ExtractHelpCommandPaths(result.StdOut).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task HelpOutput_DoesNotExposeXmlDocumentationMarkup ()
    {
        await using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        await ConsoleAppRunner.RunWithRegisteredAppAsync(serviceProvider, async app =>
        {
            var rootResult = await ConsoleAppHelpRunner.RunRootHelpAsync(app);
            AssertDoesNotExposeXmlDocumentationMarkup(rootResult);

            foreach (var commandPath in UcliCommandCatalog.CommandPaths)
            {
                var commandResult = await ConsoleAppHelpRunner.RunHelpAsync(app, commandPath);
                AssertDoesNotExposeXmlDocumentationMarkup(commandResult);
            }
        });
    }

    [Theory]
    [InlineData(UcliCommandNames.Call)]
    [InlineData(UcliCommandNames.Plan)]
    [InlineData(UcliCommandNames.Validate)]
    [InlineData(UcliCommandNames.Eval)]
    [Trait("Size", "Medium")]
    public async Task HelpOutput_WhenCommandReadsRedirectedStandardInput_DescribesInput (string commandPath)
    {
        await using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        var result = await ConsoleAppHelpRunner.RunHelpAsync(serviceProvider, commandPath);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("redirected standard input", result.StdOut, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build run", "--profilePath is required")]
    [InlineData("query assets find", "Requires at least one of --type, --pathPrefix, or --nameContains")]
    [InlineData("query scene tree", "--path is required")]
    [InlineData("query comp schema", "--type is required")]
    [InlineData("query asset schema", "Requires exactly one selector")]
    [InlineData("query go describe", "Requires exactly one target")]
    [InlineData("resolve", "Requires exactly one selector")]
    [Trait("Size", "Medium")]
    public async Task HelpOutput_WhenCommandHasRequiredInputRule_DescribesRule (
        string commandPath,
        string expectedRule)
    {
        await using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        var result = await ConsoleAppHelpRunner.RunHelpAsync(serviceProvider, commandPath);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains(expectedRule, result.StdOut, StringComparison.Ordinal);
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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task HelpOutput_MultiWordLongOptionsExposeCamelCaseAlias ()
    {
        var failures = new List<string>();
        await using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        await ConsoleAppRunner.RunWithRegisteredAppAsync(serviceProvider, async app =>
        {
            foreach (var commandPath in UcliCommandCatalog.CommandPaths.Order(StringComparer.Ordinal))
            {
                var result = await ConsoleAppHelpRunner.RunHelpAsync(app, commandPath);

                Assert.Equal((int)CliExitCode.Success, result.ExitCode);

                foreach (var optionHelpLine in ExtractOptionHelpLines(result.StdOut))
                {
                    foreach (Match match in KebabCaseLongOptionPattern.Matches(optionHelpLine))
                    {
                        var kebabCaseOption = match.Value;
                        if (HasKnownAlternativePublicOption(commandPath, kebabCaseOption, optionHelpLine))
                        {
                            continue;
                        }

                        var camelCaseOption = ToCamelCaseLongOption(kebabCaseOption);
                        if (!optionHelpLine.Contains(camelCaseOption, StringComparison.Ordinal))
                        {
                            failures.Add(
                                $"{commandPath}: expected {camelCaseOption} next to {kebabCaseOption} in `{optionHelpLine.Trim()}`.");
                        }
                    }
                }
            }
        });

        Assert.True(
            failures.Count == 0,
            "Every multi-word long option must expose its camelCase public spelling."
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures));
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

    private static IEnumerable<string> ExtractOptionHelpLines (string helpOutput)
    {
        var lines = helpOutput.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("-", StringComparison.Ordinal) && trimmed.Contains("--", StringComparison.Ordinal))
            {
                yield return line.TrimEnd('\r');
            }
        }
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

    private static bool HasKnownAlternativePublicOption (
        string commandPath,
        string kebabCaseOption,
        string optionHelpLine)
    {
        return (string.Equals(commandPath, $"{UcliCommandNames.Ops} {UcliCommandNames.ListSubcommand}", StringComparison.Ordinal)
            && string.Equals(kebabCaseOption, "--operation-kind", StringComparison.Ordinal)
            && optionHelpLine.Contains("--kind", StringComparison.Ordinal))
            || (string.Equals(commandPath, $"{UcliCommandNames.Test} {UcliCommandNames.RunSubcommand}", StringComparison.Ordinal)
            && string.Equals(kebabCaseOption, "--execution-mode", StringComparison.Ordinal)
                && optionHelpLine.Contains("--mode", StringComparison.Ordinal))
            || (commandPath.StartsWith($"{UcliCommandNames.Skills} ", StringComparison.Ordinal)
                && IsAgentSkillsGeneratedOption(kebabCaseOption));
    }

    private static bool IsAgentSkillsGeneratedOption (string kebabCaseOption)
    {
        return kebabCaseOption is "--repository-root"
            or "--target-dir"
            or "--dry-run"
            or "--print-diff";
    }

    private static string ToCamelCaseLongOption (string kebabCaseOption)
    {
        var value = kebabCaseOption[2..];
        var segments = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder(kebabCaseOption.Length);
        builder.Append("--");
        builder.Append(segments[0]);

        for (var i = 1; i < segments.Length; i++)
        {
            builder.Append(char.ToUpperInvariant(segments[i][0]));
            builder.Append(segments[i][1..]);
        }

        return builder.ToString();
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

    private static void AssertDoesNotExposeXmlDocumentationMarkup (CommandExecutionResult result)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.DoesNotContain("<c>", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("</c>", result.StdOut, StringComparison.Ordinal);
    }

}
