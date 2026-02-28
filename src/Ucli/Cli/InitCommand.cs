using ConsoleAppFramework;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Init;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>init</c> CLI command entry point. </summary>
internal sealed class InitCommand
{
    /// <summary> Gets the command name used by this command handler. </summary>
    internal const string CommandName = "init";

    private readonly IInitService initService;

    /// <summary> Initializes a new instance of the <see cref="InitCommand" /> class. </summary>
    /// <param name="initService"> The init service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="initService" /> is <see langword="null" />. </exception>
    public InitCommand (IInitService initService)
    {
        this.initService = initService ?? throw new ArgumentNullException(nameof(initService));
    }

    /// <summary> Executes the <c>init</c> command and emits the JSON result contract. </summary>
    /// <param name="force"> Whether existing template files can be overwritten. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the command pipeline. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(CommandName)]
    public async Task<int> Init (
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var executionResult = await initService.Execute(force, cancellationToken).ConfigureAwait(false);
        var result = CreateCommandResult(executionResult);
        CommandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }

    /// <summary> Creates the command-level JSON result from an init service execution result. </summary>
    /// <param name="executionResult"> The init service execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionResult" /> is <see langword="null" />. </exception>
    private static CommandResult CreateCommandResult (InitExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            var output = executionResult.Output!;
            return CommandResult.Success(
                command: CommandName,
                message: "uCLI initialization completed.",
                payload: new
                {
                    configPath = output.ConfigPath,
                    gitignorePath = output.GitIgnorePath,
                });
        }

        var error = executionResult.Error!;
        return error.Kind == ExecutionErrorKind.InvalidArgument
            ? CommandResult.InvalidArgument(CommandName, error.Message)
            : CommandResult.InternalError(CommandName, error.Message);
    }
}