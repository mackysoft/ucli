using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the validate CLI command entry point. </summary>
internal sealed class ValidateCommand
{
    private readonly IValidateService validateService;

    /// <summary> Initializes a new instance of the ValidateCommand class. </summary>
    /// <param name="validateService"> The validate workflow service dependency. </param>
    public ValidateCommand (IValidateService validateService)
    {
        this.validateService = validateService ?? throw new ArgumentNullException(nameof(validateService));
    }

    /// <summary> Executes the validate command and emits the JSON result contract. </summary>
    /// <param name="requestPath">--requestPath, Optional path to one request JSON file.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Validate)]
    public async Task<int> Validate (
        string? requestPath = null,
        string? projectPath = null,
        string? readIndexMode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedReadIndexModeResult = ReadIndexModeOptionNormalizer.Normalize(readIndexMode);
        if (!normalizedReadIndexModeResult.IsSuccess)
        {
            var errorResult = ValidateCommandResultFactory.CreateExecutionError(normalizedReadIndexModeResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var serviceResult = await validateService.Execute(
                new ValidateCommandInput(
                    RequestPath: requestPath,
                    ProjectPath: projectPath,
                    ReadIndexMode: normalizedReadIndexModeResult.Mode),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = ValidateCommandResultFactory.Create(serviceResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
