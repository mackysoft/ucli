using System.Globalization;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonCliOutputContractTests
{
    private const int DaemonListContractTestTimeoutMilliseconds = 15000;

    private static readonly Lazy<ServiceProvider> SharedDaemonServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Status_WithProjectPath_ReturnsNotRunningJsonContractAsSingleJson ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-status-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await RunDaemonStatusCommandAsync(projectPath: unityProjectPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStatus);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("daemonStatus", "notRunning")
                .IsNull("serverVersion")
                .IsNull("editorMode")
                .IsNull("lifecycleState")
                .IsNull("blockingReason")
                .IsNull("compileState")
                .IsNull("generations")
                .HasBoolean("canAcceptExecutionRequests", false)
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonStatusMilliseconds)
                .IsNull("session")
                .IsNull("diagnosis")
                .IsNull("lastLaunchAttempt"));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("runtime", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonParserErrors_ReturnInvalidArgumentErrorAsSingleJson ()
    {
        foreach (var testCase in GetDaemonParserErrorCases())
        {
            var result = await CliInProcessRunner.RunCommandAsync(testCase.Arguments);

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.True(
                result.ExitCode == (int)CliExitCode.InvalidArgument,
                $"{testCase.Name} must return invalid argument.");
            CommandResultAssert.HasInvalidArgumentEnvelope(
                outputJson.RootElement,
                testCase.ExpectedCommandName);
            CommandResultAssert.HasSingleError(
                outputJson.RootElement,
                expectedCode: "INVALID_ARGUMENT");
            if (testCase.ExpectedUnrecognizedArgument != null)
            {
                CommandResultAssert.ReportsUnrecognizedArgument(result.StdErr, testCase.ExpectedUnrecognizedArgument);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Start_WithPublicLifecycleOptions_WhenUnityPluginMarkerIsMissing_DoesNotRejectOptions ()
    {
        using var scope = TestDirectories.CreateTempScope("cli-output-contract", "daemon-start-public-lifecycle-options");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");

        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            UcliCommandNames.StartSubcommand,
            UcliContractConstants.CliOption.ProjectPath,
            unityProjectPath,
            UcliContractConstants.CliOption.OnStartupBlocked,
            "keep",
            UcliContractConstants.CliOption.EditorMode,
            "batchmode");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.DaemonStart);
        Assert.Contains("Unity project does not contain the uCLI Unity plugin", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("onStartupBlocked must be one of", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("editorMode must be one of", result.StdOut, StringComparison.Ordinal);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(
            result.StdErr,
            UcliContractConstants.CliOption.OnStartupBlocked,
            UcliContractConstants.CliOption.EditorMode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task List_WithProjectPath_WhenNoDaemonSessionExists_ReturnsSuccessJsonContractAsSingleJson ()
    {
        var service = new StubDaemonListService(
            DaemonListExecutionResult.Success(new DaemonListExecutionOutput(
                TimeoutMilliseconds: DaemonListContractTestTimeoutMilliseconds,
                ProjectRelativePath: "UnityProject",
                IsComplete: true,
                CompletionReason: null,
                RemainingWorktreeCount: 0,
                Items: Array.Empty<DaemonListItemOutput>())));
        var unityProjectPath = "/repo/wt-current/UnityProject";

        var result = await RunDaemonListCommandAsync(
            service,
            projectPath: unityProjectPath,
            timeout: DaemonListContractTestTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture));

        DaemonListServiceAssert.ListRequested(service, unityProjectPath, DaemonListContractTestTimeoutMilliseconds);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonList);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);

        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("timeoutMilliseconds", DaemonListContractTestTimeoutMilliseconds)
                .HasString("projectRelativePath", "UnityProject")
                .HasBoolean("isComplete", true)
                .IsNull("completionReason")
                .HasInt32("remainingWorktreeCount", 0)
                .HasArrayLength("items", 0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Daemon_WithHelpOption_ReturnsHelpOutputAndSuccessExitCode ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Daemon,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("daemon start", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("daemon cleanup", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task DaemonSubcommands_WithHelpOutput_IncludePublicOptions ()
    {
        var failures = new List<string>();
        await ConsoleAppRunner.RunWithRegisteredAppAsync(SharedDaemonServiceProvider.Value, async app =>
        {
            foreach (var (subcommand, expectedOptions) in GetDaemonSubcommandHelpOptionCases())
            {
                var result = await ConsoleAppHelpRunner.RunHelpAsync(
                    app,
                    $"{UcliCommandNames.Daemon} {subcommand}");

                Assert.Equal((int)CliExitCode.Success, result.ExitCode);
                foreach (string expectedOption in expectedOptions)
                {
                    if (!result.StdOut.Contains(expectedOption, StringComparison.Ordinal))
                    {
                        failures.Add($"{subcommand}: missing `{expectedOption}`.");
                    }
                }
            }
        });

        Assert.True(
            failures.Count == 0,
            "Daemon subcommand help output must expose the public options."
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures));
    }

    private static (string Subcommand, string[] ExpectedOptions)[] GetDaemonSubcommandHelpOptionCases ()
    {
        return
        [
            (UcliCommandNames.StartSubcommand, ["-p, --projectPath", "--editorMode", "--onStartupBlocked"]),
            (UcliCommandNames.StopSubcommand, ["-p, --projectPath"]),
            (UcliCommandNames.Status, ["-p, --projectPath"]),
            (UcliCommandNames.CleanupSubcommand, ["-p, --projectPath"]),
            (UcliCommandNames.ListSubcommand, ["-p, --projectPath"]),
        ];
    }

    private static DaemonParserErrorCase[] GetDaemonParserErrorCases ()
    {
        return
        [
            new(
                "daemon without subcommand",
                [UcliCommandNames.Daemon],
                UcliCommandNames.Daemon,
                ExpectedUnrecognizedArgument: null),
            new(
                "daemon unknown option",
                [UcliCommandNames.Daemon, UcliContractConstants.CliOption.Unknown],
                UcliCommandNames.Daemon,
                ExpectedUnrecognizedArgument: null),
            new(
                "daemon unknown subcommand",
                [UcliCommandNames.Daemon, "foo"],
                UcliCommandNames.Daemon,
                ExpectedUnrecognizedArgument: null),
            new(
                "daemon start unknown option",
                [UcliCommandNames.Daemon, UcliCommandNames.StartSubcommand, UcliContractConstants.CliOption.Unknown],
                UcliCommandNames.DaemonStart,
                UcliContractConstants.CliOption.Unknown),
        ];
    }

    private static Task<CommandExecutionResult> RunDaemonStatusCommandAsync (
        string? projectPath = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<DaemonStatusCommand>(
                    SharedDaemonServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .StatusAsync(
                    projectPath: projectPath,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
    }

    private static Task<CommandExecutionResult> RunDaemonListCommandAsync (
        IDaemonListService daemonListService,
        string? projectPath = null,
        string? timeout = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<DaemonListCommand>(
                    SharedDaemonServiceProvider.Value,
                    daemonListService,
                    CommandResultTestWriter.Create())
                .ListAsync(
                    projectPath: projectPath,
                    timeout: timeout,
                    cancellationToken: CancellationToken.None));
    }

    private sealed record DaemonParserErrorCase (
        string Name,
        string[] Arguments,
        string ExpectedCommandName,
        string? ExpectedUnrecognizedArgument);

}
