using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Provides the verify CLI command entry point. </summary>
internal sealed class VerifyCommand
{
    private readonly IVerifyService verifyService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="VerifyCommand" /> class. </summary>
    public VerifyCommand (
        IVerifyService verifyService,
        ICommandResultWriter commandResultWriter)
    {
        this.verifyService = verifyService ?? throw new ArgumentNullException(nameof(verifyService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the verify command and emits the JSON result contract. </summary>
    /// <param name="profile"> Built-in verify profile name. </param>
    /// <param name="profilePath">--profilePath, Repository-local JSON verify profile path. </param>
    /// <param name="from"> Public uCLI result JSON used as post-read input. </param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode"> Unity execution mode (auto|daemon|oneshot). </param>
    /// <param name="timeout"> Timeout in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Verify)]
    public async Task<int> VerifyAsync (
        string? profile = null,
        string? profilePath = null,
        string? from = null,
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var modeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!modeResult.IsSuccess)
        {
            var errorResult = VerifyCommandResultFactory.CreateExecutionError(modeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var errorResult = VerifyCommandResultFactory.CreateExecutionError(timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var executionResult = await verifyService.ExecuteAsync(
                new VerifyCommandInput(
                    ProjectPath: projectPath,
                    Profile: profile,
                    ProfilePath: profilePath,
                    FromPath: from,
                    Mode: modeResult.Mode,
                    TimeoutMilliseconds: timeoutResult.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = VerifyCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
