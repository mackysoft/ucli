using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Errors;

/// <summary> Provides the <c>errors list</c> CLI command entry point. </summary>
internal sealed class ErrorsListCommand
{
    private readonly IErrorCodeCatalogService catalogService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="ErrorsListCommand" /> class. </summary>
    /// <param name="catalogService"> The error-code catalog service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public ErrorsListCommand (
        IErrorCodeCatalogService catalogService,
        ICommandResultWriter commandResultWriter)
    {
        this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>errors list</c> and emits the JSON result contract. </summary>
    /// <param name="category"> Optional exact category filter. </param>
    /// <param name="command"> Optional exact or family command filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ListSubcommand)]
    public int List (
        string? category = null,
        string? command = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var result = catalogService.List(new ErrorCodeCatalogListInput(category, command));
        var commandResult = ErrorsCommandResultFactory.CreateList(result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
