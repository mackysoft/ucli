using ConsoleAppFramework;
using MackySoft.Ucli.Features.Testing.Profiles.UseCases.ProfileInit;

namespace MackySoft.Ucli.Hosting.Cli;

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
    /// <param name="outputPath"> -o|--outputPath, Output path for profile JSON. Defaults to <c>test.profile.json</c> when omitted. </param>
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

        var input = new TestProfileInitCommandInput(
            OutputPath: outputPath,
            Force: force);
        var executionResult = await testProfileInitService.Execute(input, cancellationToken).ConfigureAwait(false);
        var result = TestProfileInitCommandResultFactory.Create(executionResult);
        CommandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}