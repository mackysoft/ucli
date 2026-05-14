using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Provides the <c>codes list</c> CLI command entry point. </summary>
internal sealed class CodesListCommand
{
    private readonly ICodeCatalogService catalogService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="CodesListCommand" /> class. </summary>
    /// <param name="catalogService"> The code catalog service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public CodesListCommand (
        ICodeCatalogService catalogService,
        ICommandResultWriter commandResultWriter)
    {
        this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>codes list</c> and emits the JSON result contract. </summary>
    /// <param name="kind"> Optional exact kind filter. </param>
    /// <param name="command"> Optional exact or family command filter. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ListSubcommand)]
    public int List (
        string? kind = null,
        string? command = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var result = catalogService.List(new CodeCatalogListInput(kind, command));
        var commandResult = CodesCommandResultFactory.CreateList(result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
