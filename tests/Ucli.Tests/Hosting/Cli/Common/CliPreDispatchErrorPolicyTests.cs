using MackySoft.Ucli.Hosting.Cli.Common.Parsing;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliPreDispatchErrorPolicyTests
{
    private static readonly InvalidArgumentCase[] InvalidArgumentCases =
    [
        new(
            ["unknown"],
            UcliCommandNames.Root,
            "Command 'unknown' is not recognized."),
        new(
            ["help"],
            UcliCommandNames.Root,
            "Command 'help' is not recognized."),
        new(
            [UcliCommandNames.Build],
            UcliCommandNames.Build,
            "Subcommand is required for command 'build'. Supported subcommands: run."),
        new(
            [UcliCommandNames.Build, "unknown"],
            UcliCommandNames.Build,
            "Subcommand 'unknown' is not recognized for command 'build'."),
        new(
            [UcliCommandNames.Daemon],
            UcliCommandNames.Daemon,
            "Subcommand is required for command 'daemon'. Supported subcommands: start, stop, cleanup, status, list."),
        new(
            [UcliCommandNames.Daemon, "unknown"],
            UcliCommandNames.Daemon,
            "Subcommand 'unknown' is not recognized for command 'daemon'."),
        new(
            [UcliCommandNames.Daemon, "--help", "extra"],
            UcliCommandNames.Daemon,
            "Argument 'extra' is not recognized."),
        new(
            [UcliCommandNames.Logs],
            UcliCommandNames.Logs,
            "Subcommand is required for command 'logs'. Supported subcommands: daemon, unity."),
        new(
            [UcliCommandNames.Logs, UcliCommandNames.UnitySubcommand],
            UcliCommandNames.Logs,
            "Subcommand is required for command 'logs unity'. Supported subcommands: read, clear."),
        new(
            [UcliCommandNames.Logs, UcliCommandNames.UnitySubcommand, "--version", "extra"],
            UcliCommandNames.Logs,
            "Argument 'extra' is not recognized."),
        new(
            [UcliCommandNames.Logs, UcliCommandNames.Daemon],
            UcliCommandNames.Logs,
            "Subcommand is required for command 'logs daemon'. Supported subcommands: read."),
        new(
            [UcliCommandNames.Logs, UcliCommandNames.UnitySubcommand, "--tail", "1"],
            UcliCommandNames.Logs,
            "Subcommand '--tail' is not recognized for command 'logs unity'."),
        new(
            [UcliCommandNames.Logs, UcliCommandNames.Daemon, "--tail", "1"],
            UcliCommandNames.Logs,
            "Subcommand '--tail' is not recognized for command 'logs daemon'."),
        new(
            [UcliCommandNames.Logs, UcliCommandNames.Daemon, UcliCommandNames.ClearSubcommand],
            UcliCommandNames.Logs,
            "Subcommand 'clear' is not recognized for command 'logs daemon'."),
        new(
            [UcliCommandNames.Ops, "unknown"],
            UcliCommandNames.Ops,
            "Subcommand 'unknown' is not recognized for command 'ops'."),
        new(
            [UcliCommandNames.Codes],
            UcliCommandNames.Codes,
            "Subcommand is required for command 'codes'. Supported subcommands: list, describe."),
        new(
            [UcliCommandNames.Codes, "unknown"],
            UcliCommandNames.Codes,
            "Subcommand 'unknown' is not recognized for command 'codes'."),
        new(
            [UcliCommandNames.Play],
            UcliCommandNames.Play,
            "Subcommand is required for command 'play'. Supported subcommands: status, enter, exit."),
        new(
            [UcliCommandNames.Play, "unknown"],
            UcliCommandNames.Play,
            "Subcommand 'unknown' is not recognized for command 'play'."),
        new(
            ["errors"],
            UcliCommandNames.Root,
            "Command 'errors' is not recognized."),
        new(
            ["errors", "list"],
            UcliCommandNames.Root,
            "Command 'errors' is not recognized."),
        new(
            [UcliCommandNames.Skills, "unknown"],
            UcliCommandNames.Skills,
            "Subcommand 'unknown' is not recognized for command 'skills'."),
        new(
            [UcliCommandNames.Skills, UcliCommandNames.ListSubcommand, "extra"],
            UcliCommandNames.SkillsList,
            "Argument 'extra' is not recognized."),
        new(
            [UcliCommandNames.Query],
            UcliCommandNames.Query,
            "Subcommand is required for command 'query'. Supported subcommands: assets, scene, go, comp, asset."),
        new(
            [UcliCommandNames.Query, "unknown"],
            UcliCommandNames.Query,
            "Subcommand 'unknown' is not recognized for command 'query'."),
        new(
            [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand],
            UcliCommandNames.Query,
            "Subcommand is required for command 'query assets'. Supported subcommands: find."),
        new(
            [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, "unknown"],
            UcliCommandNames.Query,
            "Subcommand 'unknown' is not recognized for command 'query assets'."),
        new(
            [UcliCommandNames.Test],
            UcliCommandNames.Test,
            "Subcommand is required for command 'test'. Supported subcommands: run, profile."),
        new(
            [UcliCommandNames.Test, "unknown"],
            UcliCommandNames.Test,
            "Subcommand 'unknown' is not recognized for command 'test'."),
        new(
            [UcliCommandNames.Test, UcliCommandNames.Profile],
            UcliCommandNames.Test,
            "Subcommand is required for command 'test profile'. Supported subcommands: init."),
        new(
            [UcliCommandNames.Test, UcliCommandNames.Profile, "unknown"],
            UcliCommandNames.Test,
            "Subcommand 'unknown' is not recognized for command 'test profile'."),
    ];

    private static readonly string[][] PassThroughCases =
    [
        [],
        ["--help"],
        [UcliCommandNames.Status],
        [UcliCommandNames.Daemon, "--help"],
        [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, "--help"],
        [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, UcliCommandNames.FindSubcommand],
        [UcliCommandNames.Build, "--help"],
        [UcliCommandNames.Test, "--help"],
        [UcliCommandNames.Test, UcliCommandNames.Profile, "--help"],
        [UcliCommandNames.Test, UcliCommandNames.Profile, UcliCommandNames.InitSubcommand],
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateErrorResult_WhenCommandShouldStopBeforeDispatch_ReturnsInvalidArgument ()
    {
        foreach (var testCase in InvalidArgumentCases)
        {
            var result = CliPreDispatchErrorPolicy.TryCreateErrorResult(testCase.Args);

            AssertInvalidArgument(result, testCase.ExpectedCommand, testCase.ExpectedMessage);
        }
    }

    private static void AssertInvalidArgument (
        CommandResult? result,
        string expectedCommand,
        string expectedMessage)
    {
        Assert.NotNull(result);
        Assert.Equal(expectedCommand, result.Command);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Equal(expectedMessage, result.Message);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateErrorResult_WhenFrameworkShouldDispatch_ReturnsNull ()
    {
        foreach (var args in PassThroughCases)
        {
            var result = CliPreDispatchErrorPolicy.TryCreateErrorResult(args);

            Assert.True(result is null, $"Expected '{string.Join(' ', args)}' to pass through.");
        }
    }

    private sealed record InvalidArgumentCase (
        string[] Args,
        string ExpectedCommand,
        string ExpectedMessage);
}
