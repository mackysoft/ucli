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
                [UcliCommandNames.Query, "unknown"],
                UcliCommandNames.Query,
                "Subcommand 'unknown' is not recognized for command 'query'."
            },
            {
                [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand],
                UcliCommandNames.Query,
                "Subcommand is required for command 'query assets'. Supported subcommands: find."
            },
            {
                [UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, "unknown"],
                UcliCommandNames.Query,
                "Subcommand 'unknown' is not recognized for command 'query assets'."
            },
            {
                [UcliCommandNames.Test],
                UcliCommandNames.Test,
                "Subcommand is required for command 'test'. Supported subcommands: run, profile."
            },
            {
                [UcliCommandNames.Test, "unknown"],
                UcliCommandNames.Test,
                "Subcommand 'unknown' is not recognized for command 'test'."
            },
            {
                [UcliCommandNames.Test, UcliCommandNames.Profile],
                UcliCommandNames.Test,
                "Subcommand is required for command 'test profile'. Supported subcommands: init."
            },
            {
                [UcliCommandNames.Test, UcliCommandNames.Profile, "unknown"],
                UcliCommandNames.Test,
                "Subcommand 'unknown' is not recognized for command 'test profile'."
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

        Assert.NotNull(result);
        Assert.Equal(expectedCommand, result.Command);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Equal(expectedMessage, result.Message);
        var error = Assert.Single(result.Errors);
        Assert.Equal("INVALID_ARGUMENT", error.Code);
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
