using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class PlayCliOutputContractTests
{
    private static readonly Lazy<ServiceProvider> SharedPlayServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Theory]
    [InlineData(UcliCommandNames.EnterSubcommand, UcliCommandNames.PlayEnter)]
    [InlineData(UcliCommandNames.ExitSubcommand, UcliCommandNames.PlayExit)]
    [Trait("Size", "Medium")]
    public async Task PlayLifecycleCommand_WithAllowPlayModeOption_ReturnsInvalidArgument (
        string subcommand,
        string expectedCommand)
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Play,
            subcommand,
            "--allowPlayMode");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, expectedCommand);
        CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, UcliContractConstants.CliOption.AllowPlayMode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PlayCommands_WithProjectPathAndTimeoutOptions_WhenGuiSessionIsMissing_ReturnSessionNotAvailable ()
    {
        using var scope = TestDirectories.CreateTempScope("play-cli-output-contract", "session-not-available");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        foreach (var testCase in GetMissingGuiSessionCases())
        {
            var result = await testCase.ExecuteAsync(unityProjectPath);

            JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("play", testCase.GoldenFileName), result.StdOut);
            Assert.True(
                result.ExitCode == (int)CliExitCode.ToolError,
                $"{testCase.CommandName} must return tool error when the GUI session is missing.");
            testCase.AssertAdditionalContract(result);
        }
    }

    private static Task<CommandExecutionResult> RunPlayEnterCommandAsync (
        string? projectPath = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<PlayEnterCommand>(
                    SharedPlayServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .EnterAsync(
                    projectPath: projectPath,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
    }

    private static Task<CommandExecutionResult> RunPlayExitCommandAsync (
        string? projectPath = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<PlayExitCommand>(
                    SharedPlayServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .ExitAsync(
                    projectPath: projectPath,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
    }

    private static MissingGuiSessionCase[] GetMissingGuiSessionCases ()
    {
        return
        [
            new(
                UcliCommandNames.PlayStatus,
                "status-session-not-available.json",
                unityProjectPath => CliInProcessRunner.RunCommandAsync(
                    UcliCommandNames.Play,
                    UcliCommandNames.Status,
                    "--projectPath",
                    unityProjectPath,
                    "--timeout",
                    "1000"),
                result => CommandResultAssert.DoesNotReportUnrecognizedArguments(
                    result.StdErr,
                    UcliContractConstants.CliOption.ProjectPath,
                    UcliContractConstants.CliOption.Timeout)),
            new(
                UcliCommandNames.PlayEnter,
                "enter-session-not-available.json",
                unityProjectPath => RunPlayEnterCommandAsync(
                    projectPath: unityProjectPath,
                    timeout: "1000"),
                static _ => { }),
            new(
                UcliCommandNames.PlayExit,
                "exit-session-not-available.json",
                unityProjectPath => RunPlayExitCommandAsync(
                    projectPath: unityProjectPath,
                    timeout: "1000"),
                static _ => { }),
        ];
    }

    private sealed record MissingGuiSessionCase (
        string CommandName,
        string GoldenFileName,
        Func<string, Task<CommandExecutionResult>> ExecuteAsync,
        Action<CommandExecutionResult> AssertAdditionalContract);
}
