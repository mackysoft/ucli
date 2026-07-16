using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Provides the <c>codes describe</c> CLI command entry point. </summary>
internal sealed class CodesDescribeCommand
{
    private readonly ICodeCatalogService catalogService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="CodesDescribeCommand" /> class. </summary>
    /// <param name="catalogService"> The code catalog service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public CodesDescribeCommand (
        ICodeCatalogService catalogService,
        ICommandResultWriter commandResultWriter)
    {
        this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Describes one machine-readable code and emits the JSON result contract. </summary>
    /// <param name="code"> The target code or kind-qualified code reference. </param>
    /// <param name="requireKnown">--requireKnown, Rejects codes that are absent from this client's catalog. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.DescribeSubcommand)]
    public int Describe (
        [Argument]
        string code,
        bool requireKnown = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var result = CodeCliArgumentParser.TryParse(code, out var reference, out var errorMessage)
            ? catalogService.Describe(reference, requireKnown)
            : CodeCatalogDescribeResult.Failure(ExecutionError.InvalidArgument(
                errorMessage,
                UcliCoreErrorCodes.InvalidArgument));
        var commandResult = CodesCommandResultFactory.CreateDescribe(result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
