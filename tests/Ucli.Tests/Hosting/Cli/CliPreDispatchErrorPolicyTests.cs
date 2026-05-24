using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Parsing;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliPreDispatchErrorPolicyTests
{
    public static TheoryData<string[], string, string> InvalidArgumentCases => new()
        {
            {
                ["unknown"],
                UcliCommandNames.Root,
                "Command 'unknown' is not recognized."
            },
            {
                [UcliCommandNames.Daemon],
                UcliCommandNames.Daemon,
                "Subcommand is required for command 'daemon'. Supported subcommands: start, stop, cleanup, status, list."
            },
            {
                [UcliCommandNames.Daemon, "unknown"],
                UcliCommandNames.Daemon,
                "Subcommand 'unknown' is not recognized for command 'daemon'."
            },
            {
                [UcliCommandNames.Logs],
                UcliCommandNames.Logs,
                "Subcommand is required for command 'logs'. Supported subcommands: daemon, unity."
            },
            {
                [UcliCommandNames.Ops, "unknown"],
                UcliCommandNames.Ops,
                "Subcommand 'unknown' is not recognized for command 'ops'."
            },
            {
                [UcliCommandNames.Codes],
                UcliCommandNames.Codes,
                "Subcommand is required for command 'codes'. Supported subcommands: list, describe."
            },
            {
                [UcliCommandNames.Codes, "unknown"],
                UcliCommandNames.Codes,
                "Subcommand 'unknown' is not recognized for command 'codes'."
            },
            {
                [UcliCommandNames.Play],
                UcliCommandNames.Play,
                "Subcommand is required for command 'play'. Supported subcommands: status, enter, exit."
            },
            {
                [UcliCommandNames.Play, "unknown"],
                UcliCommandNames.Play,
                "Subcommand 'unknown' is not recognized for command 'play'."
            },
            {
                ["errors"],
                UcliCommandNames.Root,
                "Command 'errors' is not recognized."
            },
            {
                ["errors", "list"],
                UcliCommandNames.Root,
                "Command 'errors' is not recognized."
            },
            {
                [UcliCommandNames.Skills, "unknown"],
                UcliCommandNames.Skills,
                "Subcommand 'unknown' is not recognized for command 'skills'."
            },
            {
                [UcliCommandNames.Skills, UcliCommandNames.ListSubcommand, "extra"],
                UcliCommandNames.SkillsList,
                "Argument 'extra' is not recognized."
            },
            {
                [UcliCommandNames.Query],
                UcliCommandNames.Query,
                "Subcommand is required for command 'query'. Supported subcommands: assets, scene, go, comp, asset."
            },
            {
                [UcliCommandNames.Query, "unknown"],
                UcliCommandNames.Query,
                "Subcommand 'unknown' is not recognized for command 'query'."
            },
        };

    public static TheoryData<string[]> PassThroughCases => new()
        {
            { [] },
            { ["--help"] },
            { [UcliCommandNames.Help] },
            { [UcliCommandNames.Status] },
            { [UcliCommandNames.Daemon, "--help"] },
            { [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, "--help"] },
            { [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, UcliCommandNames.FindSubcommand] },
            { [UcliCommandNames.Test] },
            { [UcliCommandNames.Test, "unknown"] },
            { [UcliCommandNames.Test, UcliCommandNames.Profile] },
            { [UcliCommandNames.Test, UcliCommandNames.Profile, "unknown"] },
            { [UcliCommandNames.Test, UcliCommandNames.Profile, "--help"] },
            { [UcliCommandNames.Test, UcliCommandNames.Profile, UcliCommandNames.InitSubcommand] },
        };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidArgumentCases))]
    public void TryCreateErrorResult_WhenCommandShouldStopBeforeDispatch_ReturnsInvalidArgument (
        string[] args,
        string expectedCommand,
        string expectedMessage)
    {
        var result = CliPreDispatchErrorPolicy.TryCreateErrorResult(args);

        AssertInvalidArgument(result, expectedCommand, expectedMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateErrorResult_WhenQueryLeafIsMissingOrUnknown_ReturnsInvalidArgument ()
    {
        var missingLeafResult = CliPreDispatchErrorPolicy.TryCreateErrorResult(
            [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand]);
        var expectedMissingLeafMessage =
            "Subcommand is required for command 'query assets'. Supported subcommands: find.";

        AssertInvalidArgument(missingLeafResult, UcliCommandNames.Query, expectedMissingLeafMessage);

        var unknownLeafResult = CliPreDispatchErrorPolicy.TryCreateErrorResult(
            [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, "unknown"]);
        var expectedUnknownLeafMessage = "Subcommand 'unknown' is not recognized for command 'query assets'.";

        AssertInvalidArgument(unknownLeafResult, UcliCommandNames.Query, expectedUnknownLeafMessage);
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

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(PassThroughCases))]
    public void TryCreateErrorResult_WhenFrameworkShouldDispatch_ReturnsNull (string[] args)
    {
        var result = CliPreDispatchErrorPolicy.TryCreateErrorResult(args);

        Assert.Null(result);
    }
}
