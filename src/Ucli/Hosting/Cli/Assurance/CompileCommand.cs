using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Provides the compile CLI command entry point. </summary>
internal sealed class CompileCommand
{
    private readonly ICompileService compileService;

    private readonly ICommandResultWriter commandResultWriter;

    private readonly CliStreamEntryWriterFactory streamEntryWriterFactory;

    private readonly ILifecycleExecutionCliInvocationFactory invocationFactory;

    /// <summary> Initializes a new instance of the <see cref="CompileCommand" /> class. </summary>
    public CompileCommand (
        ICompileService compileService,
        ICommandResultWriter commandResultWriter,
        CliStreamEntryWriterFactory streamEntryWriterFactory,
        ILifecycleExecutionCliInvocationFactory invocationFactory)
    {
        this.compileService = compileService ?? throw new ArgumentNullException(nameof(compileService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.streamEntryWriterFactory = streamEntryWriterFactory ?? throw new ArgumentNullException(nameof(streamEntryWriterFactory));
        this.invocationFactory = invocationFactory ?? throw new ArgumentNullException(nameof(invocationFactory));
    }

    /// <summary> Executes the compile command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="format"> Progress entry format (text|json). </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Compile)]
    public async Task<int> CompileAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CommandExecutionState.MarkStarted();

        var formatResult = CliStreamEntryFormatOptionNormalizer.Normalize(format);
        if (!formatResult.IsSuccess)
        {
            var errorResult = CompileCommandResultFactory.CreateExecutionError(formatResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var modeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!modeResult.IsSuccess)
        {
            var errorResult = CompileCommandResultFactory.CreateExecutionError(modeResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var errorResult = CompileCommandResultFactory.CreateExecutionError(timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var progressSink = new CliCommandProgressSink(
            formatResult.Format,
            streamEntryWriterFactory.Create(UcliCommandNames.Compile),
            new CompileProgressTextProjector());
        var invocationResult = await invocationFactory.CreateCompileStartAsync(
                projectPath,
                modeResult.Mode ?? UnityExecutionMode.Auto,
                timeoutResult.TimeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        var executionResult = invocationResult.IsSuccess
            ? await compileService.StartAsync(
                    invocationResult.Invocation!,
                    progressSink,
                    cancellationToken)
                .ConfigureAwait(false)
            : CompileExecutionResult.Failed(
                invocationResult.Failure!,
                invocationResult.Project,
                lifecycleExecutionRef: null,
                ExecutionApplicationState.NotApplied);
        var commandResult = CompileCommandResultFactory.Create(executionResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
