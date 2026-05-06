using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Provides the test profile init CLI command entry point. </summary>
internal sealed class TestProfileInitCommand
{
    private readonly ITestProfileInitService testProfileInitService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the TestProfileInitCommand class. </summary>
    /// <param name="testProfileInitService"> The test-profile init service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when testProfileInitService is null. </exception>
    public TestProfileInitCommand (
        ITestProfileInitService testProfileInitService,
        ICommandResultWriter? commandResultWriter = null)
    {
        this.testProfileInitService = testProfileInitService ?? throw new ArgumentNullException(nameof(testProfileInitService));
        this.commandResultWriter = commandResultWriter ?? CommandResultWriter.CreateDefault();
    }

    /// <summary> Executes the test profile init command and emits the JSON result contract. </summary>
    /// <param name="outputPath"> -o|--outputPath, Output path for profile JSON. Defaults to test.profile.json when omitted. </param>
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
        var executionResult = await testProfileInitService.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        var result = TestProfileInitCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
