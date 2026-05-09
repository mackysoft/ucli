using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Provides the test run CLI command entry point. </summary>
internal sealed class TestRunCommand
{
    private readonly ITestRunService testRunService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the TestRunCommand class. </summary>
    /// <param name="testRunService"> The test-run core service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when testRunService is null. </exception>
    public TestRunCommand (
        ITestRunService testRunService,
        ICommandResultWriter commandResultWriter)
    {
        this.testRunService = testRunService ?? throw new ArgumentNullException(nameof(testRunService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the test run command and emits the JSON result contract. </summary>
    /// <param name="projectPath"> -p|--projectPath, Unity project root path. </param>
    /// <param name="profilePath"> -c|--profilePath, profile configuration path. </param>
    /// <param name="executionMode"> --mode, Unity execution mode (auto|daemon|oneshot). </param>
    /// <param name="unityVersion"> -u|--unityVersion, Unity editor version. </param>
    /// <param name="unityEditorPath"> --unityEditorPath, Unity editor executable path or editor directory path. </param>
    /// <param name="testPlatform"> --testPlatform, Unity test platform (editmode, playmode, or a Unity BuildTarget literal). </param>
    /// <param name="testFilter"> -f|--testFilter, test name filter pattern. </param>
    /// <param name="testCategory"> --testCategory, comma-separated test categories. </param>
    /// <param name="assemblyName"> -a|--assemblyName, comma-separated assembly names. </param>
    /// <param name="testSettingsPath"> -s|--testSettingsPath, path to TestSettings.json. </param>
    /// <param name="timeout"> Timeout in milliseconds. </param>
    /// <param name="failFast"> --failFast, Fails immediately when daemon-backed execution is not yet ready. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the command pipeline. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.RunSubcommand)]
    public async Task<int> RunAsync (
        string? projectPath = null,
        string? profilePath = null,
        string? executionMode = null,
        string? unityVersion = null,
        string? unityEditorPath = null,
        string? testPlatform = null,
        string? testFilter = null,
        string? testCategory = null,
        string? assemblyName = null,
        string? testSettingsPath = null,
        int? timeout = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(executionMode);
        if (!normalizedModeResult.IsSuccess)
        {
            var errorResult = TestRunCommandResultFactory.Create(TestRunServiceResult.InvalidInput(
                normalizedModeResult.Error!.Message,
                UcliCoreErrorCodes.InvalidArgument));
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTestPlatformResult = TestRunPlatformOptionNormalizer.Normalize(testPlatform);
        if (!normalizedTestPlatformResult.IsSuccess)
        {
            var errorResult = TestRunCommandResultFactory.Create(TestRunServiceResult.InvalidInput(
                normalizedTestPlatformResult.Error!.Message,
                UcliCoreErrorCodes.InvalidArgument));
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var serviceResult = await testRunService.ExecuteAsync(
            new TestRunCommandInput(
                ProjectPath: projectPath,
                ProfilePath: profilePath,
                Mode: normalizedModeResult.Mode,
                UnityVersion: unityVersion,
                UnityEditorPath: unityEditorPath,
                TestPlatform: normalizedTestPlatformResult.TestPlatform,
                TestFilter: testFilter,
                TestCategory: SplitCommaSeparatedValues(testCategory),
                AssemblyName: SplitCommaSeparatedValues(assemblyName),
                TestSettingsPath: testSettingsPath,
                TimeoutMilliseconds: timeout,
                FailFast: failFast),
            cancellationToken).ConfigureAwait(false);
        var commandResult = TestRunCommandResultFactory.Create(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    internal static string[]? SplitCommaSeparatedValues (string? value)
    {
        if (value is null)
        {
            return null;
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
