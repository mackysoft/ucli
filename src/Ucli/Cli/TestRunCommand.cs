using ConsoleAppFramework;
using MackySoft.Ucli.TestRun;
using MackySoft.Ucli.TestRun.Service;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>test run</c> CLI command entry point. </summary>
internal sealed class TestRunCommand
{
    private readonly ITestRunService testRunService;

    /// <summary> Initializes a new instance of the <see cref="TestRunCommand" /> class. </summary>
    /// <param name="testRunService"> The test-run core service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="testRunService" /> is <see langword="null" />. </exception>
    public TestRunCommand (ITestRunService testRunService)
    {
        this.testRunService = testRunService ?? throw new ArgumentNullException(nameof(testRunService));
    }

    /// <summary> Executes the <c>test run</c> command and emits the JSON result contract. </summary>
    /// <param name="projectPath"> -p|--projectPath, Unity project root path. </param>
    /// <param name="profilePath"> -c|--profilePath, profile configuration path. </param>
    /// <param name="executionMode"> --mode, Unity execution mode (<c>auto|daemon|oneshot</c>). </param>
    /// <param name="unityVersion"> -u|--unityVersion, Unity editor version. </param>
    /// <param name="unityEditorPath"> --unityEditorPath, Unity editor executable path or editor directory path. </param>
    /// <param name="testPlatform"> --testPlatform, Unity test platform (<c>editmode|playmode</c>). </param>
    /// <param name="buildTarget"> -t|--buildTarget, Unity build target used by PlayMode tests. </param>
    /// <param name="testFilter"> -f|--testFilter, test name filter pattern. </param>
    /// <param name="testCategory"> --testCategory, comma-separated test categories. </param>
    /// <param name="assemblyName"> -a|--assemblyName, comma-separated assembly names. </param>
    /// <param name="testSettingsPath"> -s|--testSettingsPath, path to <c>TestSettings.json</c>. </param>
    /// <param name="timeout"> Timeout in milliseconds. </param>
    /// <param name="failFast"> Fails immediately when daemon-backed execution is not yet <c>ready</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the command pipeline. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.RunSubcommand)]
    public async Task<int> Run (
        string? projectPath = null,
        string? profilePath = null,
        string? executionMode = null,
        string? unityVersion = null,
        string? unityEditorPath = null,
        string? testPlatform = null,
        string? buildTarget = null,
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

        var serviceResult = await testRunService.Execute(
            new TestRunCommandInput(
                ProjectPath: projectPath,
                ProfilePath: profilePath,
                Mode: executionMode,
                UnityVersion: unityVersion,
                UnityEditorPath: unityEditorPath,
                TestPlatform: testPlatform,
                BuildTarget: buildTarget,
                TestFilter: testFilter,
                TestCategory: SplitCommaSeparatedValues(testCategory),
                AssemblyName: SplitCommaSeparatedValues(assemblyName),
                TestSettingsPath: testSettingsPath,
                TimeoutMilliseconds: timeout,
                FailFast: failFast),
            cancellationToken).ConfigureAwait(false);
        var commandResult = TestRunCommandResultFactory.Create(serviceResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
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