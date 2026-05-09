using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Errors;

/// <summary> Provides the <c>errors describe</c> CLI command entry point. </summary>
internal sealed class ErrorsDescribeCommand
{
    private readonly IErrorCodeCatalogService catalogService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="ErrorsDescribeCommand" /> class. </summary>
    /// <param name="catalogService"> The error-code catalog service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public ErrorsDescribeCommand (
        IErrorCodeCatalogService catalogService,
        ICommandResultWriter commandResultWriter)
    {
        this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>errors describe</c> and emits the JSON result contract. </summary>
    /// <param name="code"> The target error code. </param>
    /// <param name="requireKnown"> Rejects codes that are absent from this client's catalog. </param>
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

        var result = UcliErrorCode.TryCreate(code, out var errorCode)
            ? catalogService.Describe(errorCode, requireKnown)
            : ErrorCodeCatalogDescribeResult.Failure(ExecutionError.InvalidArgument(
                "Error code must not be empty.",
                UcliCoreErrorCodes.InvalidArgument));
        var commandResult = ErrorsCommandResultFactory.CreateDescribe(result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
