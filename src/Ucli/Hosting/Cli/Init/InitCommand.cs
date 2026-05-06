using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Init.UseCases.Init;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Init;

/// <summary> Provides the init CLI command entry point. </summary>
internal sealed class InitCommand
{
    private readonly IInitService initService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the InitCommand class. </summary>
    /// <param name="initService"> The init service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when initService is null. </exception>
    public InitCommand (
        IInitService initService,
        ICommandResultWriter commandResultWriter)
    {
        this.initService = initService ?? throw new ArgumentNullException(nameof(initService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the init command and emits the JSON result contract. </summary>
    /// <param name="force"> Whether existing template files can be overwritten. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the command pipeline. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Init)]
    public async Task<int> Init (
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var input = new InitCommandInput(force);
        var executionResult = await initService.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
        var result = InitCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
