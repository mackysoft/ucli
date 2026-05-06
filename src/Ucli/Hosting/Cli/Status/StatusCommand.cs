using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Status;

/// <summary> Provides the status CLI command entry point. </summary>
internal sealed class StatusCommand
{
    private readonly IStatusService statusService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the StatusCommand class. </summary>
    /// <param name="statusService"> The status service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when statusService is null. </exception>
    public StatusCommand (
        IStatusService statusService,
        ICommandResultWriter? commandResultWriter = null)
    {
        this.statusService = statusService ?? throw new ArgumentNullException(nameof(statusService));
        this.commandResultWriter = commandResultWriter ?? CommandResultWriter.CreateDefault();
    }

    /// <summary> Executes the status command and emits the JSON result contract. </summary>
    /// <param name="projectPath">--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="timeout">Optional daemon status timeout in milliseconds. When omitted, timeout is resolved from config defaults.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Status)]
    public async Task<int> Status (
        string? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var timeoutNormalizationResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutNormalizationResult.IsSuccess)
        {
            var invalidTimeoutResult = CommandResultFactory.FromExecutionError(
                UcliCommandNames.Status,
                timeoutNormalizationResult.Error!);
            commandResultWriter.WriteToStandardOutput(invalidTimeoutResult);
            return invalidTimeoutResult.ExitCode;
        }

        var input = new StatusCommandInput(
            ProjectPath: projectPath,
            TimeoutMilliseconds: timeoutNormalizationResult.TimeoutMilliseconds);
        var executionResult = await statusService.Execute(input, cancellationToken).ConfigureAwait(false);
        var result = StatusCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
