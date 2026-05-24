using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the eval CLI command entry point. </summary>
internal sealed class EvalCommand
{
    private readonly ICallService callService;

    private readonly IEvalSourceInputReader sourceInputReader;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="EvalCommand" /> class. </summary>
    /// <param name="callService"> The call workflow service dependency. </param>
    /// <param name="sourceInputReader"> The eval source input reader dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public EvalCommand (
        ICallService callService,
        IEvalSourceInputReader sourceInputReader,
        ICommandResultWriter commandResultWriter)
    {
        this.callService = callService ?? throw new ArgumentNullException(nameof(callService));
        this.sourceInputReader = sourceInputReader ?? throw new ArgumentNullException(nameof(sourceInputReader));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes <c>eval</c> as a convenience wrapper around <c>ucli.cs.eval</c>. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="allowDangerous">--allowDangerous, Explicitly allows dangerous eval execution when the project config also permits it.</param>
    /// <param name="allowPlayMode">--allowPlayMode, Allows Play Mode mutation when the target is a GUI Editor session in Play Mode.</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="source">C# source text to evaluate.</param>
    /// <param name="file">Path to a C# source file to evaluate.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Eval)]
    public async Task<int> EvalAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        bool allowDangerous = false,
        bool allowPlayMode = false,
        bool failFast = false,
        string? source = null,
        string? file = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            return WriteExecutionError(normalizedTimeoutResult.Error!);
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            return WriteExecutionError(normalizedModeResult.Error!);
        }

        var sourceInputReadResult = await sourceInputReader.ReadAsync(source, file, cancellationToken)
            .ConfigureAwait(false);
        if (!sourceInputReadResult.IsSuccess)
        {
            return WriteExecutionError(sourceInputReadResult.Error!);
        }

        var serviceResult = await callService.ExecuteAsync(
                new CallCommandInput(
                    ProjectPath: projectPath,
                    Mode: normalizedModeResult.Mode,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
                    PlanToken: null,
                    WithPlan: true,
                    AllowDangerous: allowDangerous,
                    FailFast: failFast,
                    RequestJson: EvalRequestFactory.Create(sourceInputReadResult.Source!))
                {
                    AllowPlayMode = allowPlayMode,
                    ExecutionOwnerCommand = UcliCommandIds.Eval,
                },
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = EvalCommandResultFactory.Create(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private int WriteExecutionError (ExecutionError error)
    {
        var commandResult = CommandResultFactory.FromExecutionError(UcliCommandNames.Eval, error);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
