using ConsoleAppFramework;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestProfile;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>test profile init</c> CLI command entry point. </summary>
internal sealed class TestProfileInitCommand
{
    private readonly ITestProfileInitService testProfileInitService;

    /// <summary> Initializes a new instance of the <see cref="TestProfileInitCommand" /> class. </summary>
    /// <param name="testProfileInitService"> The test-profile init service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="testProfileInitService" /> is <see langword="null" />. </exception>
    public TestProfileInitCommand (ITestProfileInitService testProfileInitService)
    {
        this.testProfileInitService = testProfileInitService ?? throw new ArgumentNullException(nameof(testProfileInitService));
    }

    /// <summary> Executes the <c>test profile init</c> command and emits the JSON result contract. </summary>
    /// <param name="outputPath"> -o, Output path for profile JSON. Defaults to <c>test.profile.json</c> when omitted. </param>
    /// <param name="force"> -f, Whether existing profile files can be overwritten. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the command pipeline. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.InitSubcommand)]
    public async Task<int> Init (
        string? outputPath = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var executionResult = await testProfileInitService.Execute(outputPath, force, cancellationToken).ConfigureAwait(false);
        var result = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }

    /// <summary> Creates the command-level JSON result from a test-profile init service execution result. </summary>
    /// <param name="executionResult"> The test-profile init service execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionResult" /> is <see langword="null" />. </exception>
    private static CommandResult CreateCommandResult (TestProfileInitExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: UcliCommandNames.TestProfileInit,
                message: "Test profile template initialization completed.",
                payload: new
                {
                    profilePath = output.ProfilePath,
                });
        }

        var error = executionResult.Error!;
        return error.Kind == ExecutionErrorKind.InvalidArgument
            ? CommandResult.InvalidArgument(UcliCommandNames.TestProfileInit, error.Message)
            : CommandResult.InternalError(UcliCommandNames.TestProfileInit, error.Message);
    }
}
