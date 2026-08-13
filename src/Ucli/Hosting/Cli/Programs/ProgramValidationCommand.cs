using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Programs.Validate;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Programs;

/// <summary> Provides the static Program validation command entry point. </summary>
internal sealed class ProgramValidationCommand
{
    private readonly IProgramValidationService validationService;
    private readonly ProgramInputResolver inputResolver;
    private readonly ICommandResultWriter commandResultWriter;

    public ProgramValidationCommand (
        IProgramValidationService validationService,
        ProgramInputResolver inputResolver,
        ICommandResultWriter commandResultWriter)
    {
        this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        this.inputResolver = inputResolver ?? throw new ArgumentNullException(nameof(inputResolver));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Validates one Program definition without contacting Unity. </summary>
    /// <param name="programPath">--programPath, Optional root Program JSON file path.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    [Command(UcliCommandNames.Validate)]
    public async Task<int> ValidateAsync (
        string? preset = null,
        [AbsolutePathArgumentParser] AbsolutePath? programPath = null,
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var input = await inputResolver.ResolveAsync(preset, programPath, projectPath, cancellationToken).ConfigureAwait(false);
        if (input.Error is not null)
        {
            var error = ProgramCommandResultFactory.CreateValidationError(input.Error);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var result = input.HasValidationFailure
            ? ProgramDefinitionResolutionResult.Failure(input.Diagnostics)
            : input.ResolvedDefinition ?? await validationService.ValidateAsync(input.Input!, cancellationToken).ConfigureAwait(false);
        var commandResult = ProgramCommandResultFactory.CreateValidation(input.Project!.UnityProject, result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
