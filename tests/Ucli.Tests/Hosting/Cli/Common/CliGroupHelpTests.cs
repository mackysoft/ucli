namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliGroupHelpTests
{
    public static TheoryData<string, string[]> GroupHelpCases { get; } = new()
    {
        { "build", ["run"] },
        { "codes", ["describe", "list"] },
        { "daemon", ["cleanup", "list", "start", "status", "stop"] },
        { "logs", ["daemon read", "unity clear", "unity read"] },
        { "logs daemon", ["read"] },
        { "logs unity", ["clear", "read"] },
        { "ops", ["describe", "list"] },
        { "play", ["enter", "exit", "status"] },
        { "query", ["asset schema", "assets find", "comp schema", "go describe", "scene tree"] },
        { "query asset", ["schema"] },
        { "query assets", ["find"] },
        { "query comp", ["schema"] },
        { "query go", ["describe"] },
        { "query scene", ["tree"] },
        { "screenshot", ["game", "scene"] },
        { "skills", ["doctor", "export", "install", "list", "prune", "uninstall", "update"] },
        { "test", ["profile init", "run"] },
        { "test profile", ["init"] },
    };

    [Theory]
    [MemberData(nameof(GroupHelpCases))]
    [Trait("Size", "Medium")]
    public async Task GroupHelp_ListsOnlyExecutableDescendants (
        string groupPath,
        string[] expectedRelativeCommandPaths)
    {
        var result = await RunGroupHelpAsync(groupPath, "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        Assert.StartsWith($"Usage: {groupPath} <command>", result.StdOut, StringComparison.Ordinal);
        Assert.Equal(
            expectedRelativeCommandPaths.Order(StringComparer.Ordinal),
            ExtractRelativeCommandPaths(result.StdOut).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task GroupHelp_WhenShortOptionIsUsed_MatchesLongOption ()
    {
        var longResult = await RunGroupHelpAsync("logs unity", "--help");
        var shortResult = await RunGroupHelpAsync("logs unity", "-h");

        Assert.Equal(longResult.ExitCode, shortResult.ExitCode);
        Assert.Equal(longResult.StdOut, shortResult.StdOut);
        Assert.Equal(longResult.StdErr, shortResult.StdErr);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LeafHelp_RemainsFrameworkOptionHelp ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.StartsWith("Usage: daemon start [options...]", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Options:", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Commands:", result.StdOut, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("build", "unknown")]
    [InlineData("test")]
    [InlineData("test", "unknown")]
    [InlineData("test", "profile")]
    [InlineData("test", "profile", "unknown")]
    [Trait("Size", "Medium")]
    public async Task IncompleteOrUnknownGroupInvocation_ReturnsJsonInvalidArgument (params string[] args)
    {
        var result = await CliInProcessRunner.RunCommandAsync(args);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, args[0]);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task UnknownSubcommandWithHelp_IsNotTreatedAsGroupHelp ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            "unknown",
            "--help");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.Daemon);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task GroupHelpWithTrailingArgument_ReturnsJsonInvalidArgument ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            "--help",
            "extra");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.Daemon);
    }

    private static Task<CommandExecutionResult> RunGroupHelpAsync (
        string groupPath,
        string helpOption)
    {
        var args = groupPath
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Append(helpOption)
            .ToArray();
        return CliInProcessRunner.RunCommandAsync(args);
    }

    private static string[] ExtractRelativeCommandPaths (string helpOutput)
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

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("  ", StringComparison.Ordinal))
            {
                break;
            }

            commandPaths.Add(ExtractCommandPath(line));
        }

        return commandPaths.ToArray();
    }

    private static string ExtractCommandPath (string helpLine)
    {
        var trimmed = helpLine.TrimStart();
        for (var i = 0; i < trimmed.Length - 1; i++)
        {
            if (trimmed[i] == ' ' && trimmed[i + 1] == ' ')
            {
                Assert.False(string.IsNullOrWhiteSpace(trimmed[(i + 2)..]));
                return trimmed[..i];
            }
        }

        throw new InvalidOperationException($"Command help line does not contain a description separator: {helpLine}");
    }
}
