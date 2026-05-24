using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests;

public sealed class PlayCliOutputContractTests
{
    private const string AllowPlayModeOptionMessage = "Argument '--allowPlayMode' is not recognized.";

    [Theory]
    [InlineData(UcliCommandNames.EnterSubcommand, UcliCommandNames.PlayEnter)]
    [InlineData(UcliCommandNames.ExitSubcommand, UcliCommandNames.PlayExit)]
    [Trait("Size", "Medium")]
    public async Task PlayLifecycleCommand_WithAllowPlayModeOption_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Play,
            subcommand,
            "--allowPlayMode");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: expectedCommand,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
        Assert.Contains(AllowPlayModeOptionMessage, result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PlayStatus_WithProjectPathAndTimeoutOptions_WhenGuiSessionIsMissing_ReturnsSessionNotAvailable ()
    {
        using var scope = TestDirectories.CreateTempScope("play-cli-output-contract", "status-session-not-available");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Play,
            UcliCommandNames.Status,
            "--projectPath",
            unityProjectPath,
            "--timeout",
            "1000");

        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("play", "status-session-not-available.json"), result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.DoesNotContain("Argument '--projectPath' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--timeout' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PlayEnter_WithProjectPathAndTimeoutOptions_WhenGuiSessionIsMissing_ReturnsSessionNotAvailable ()
    {
        using var scope = TestDirectories.CreateTempScope("play-cli-output-contract", "enter-session-not-available");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Play,
            UcliCommandNames.EnterSubcommand,
            "--projectPath",
            unityProjectPath,
            "--timeout",
            "1000");

        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("play", "enter-session-not-available.json"), result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.DoesNotContain("Argument '--projectPath' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--timeout' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PlayExit_WithProjectPathAndTimeoutOptions_WhenGuiSessionIsMissing_ReturnsSessionNotAvailable ()
    {
        using var scope = TestDirectories.CreateTempScope("play-cli-output-contract", "exit-session-not-available");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Play,
            UcliCommandNames.ExitSubcommand,
            "--projectPath",
            unityProjectPath,
            "--timeout",
            "1000");

        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("play", "exit-session-not-available.json"), result.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.DoesNotContain("Argument '--projectPath' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--timeout' is not recognized.", result.StdErr, StringComparison.Ordinal);
    }

}
