using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Presets;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;

namespace MackySoft.Ucli.Hosting.Cli.Programs;

/// <summary> Resolves the one public Program input source and its project context. </summary>
internal sealed class ProgramInputResolver
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly IUcliConfigStore configStore;
    private readonly IProgramPresetCatalog presetCatalog;
    private readonly IProgramDefinitionFileReader fileReader;
    private readonly IRequestInputReader inputReader;

    public ProgramInputResolver (
        IProjectContextResolver projectContextResolver,
        IUcliConfigStore configStore,
        IProgramPresetCatalog presetCatalog,
        IProgramDefinitionFileReader fileReader,
        IRequestInputReader inputReader)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        this.presetCatalog = presetCatalog ?? throw new ArgumentNullException(nameof(presetCatalog));
        this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
    }

    public async ValueTask<ProgramInputResolutionResult> ResolveAsync (
        string? preset,
        AbsolutePath? programPath,
        AbsolutePath? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(preset) && programPath is not null)
        {
            return ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument("Specify exactly one of --preset, --programPath, or redirected standard input."));
        }

        var projectResult = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!projectResult.IsSuccess)
        {
            return ProgramInputResolutionResult.Failure(projectResult.Error!);
        }

        var project = projectResult.Context!;
        if (!string.IsNullOrWhiteSpace(preset))
        {
            if (Console.IsInputRedirected)
            {
                return ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument("--preset cannot be combined with redirected standard input."));
            }

            var configPath = configStore.GetConfigPath(project.UnityProject.RepositoryRoot);
            var result = await presetCatalog.ResolveAsync(
                    preset,
                    project.Config,
                    Path.GetDirectoryName(configPath.Value)!,
                    cancellationToken)
                .ConfigureAwait(false);
            return result.IsSuccess
                ? ProgramInputResolutionResult.Success(project, ProgramDefinitionResolutionResult.Success(result.Preset!.Definition))
                : ProgramInputResolutionResult.ValidationFailure(project, result.Diagnostics);
        }

        if (programPath is not null)
        {
            if (Console.IsInputRedirected)
            {
                return ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument("--programPath cannot be combined with redirected standard input."));
            }
            if (!programPath.TryGetParent(out var parent))
            {
                return ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument("--programPath must identify a file with a parent directory."));
            }
            var receipt = await ProgramDefinitionRootFileReceipt.ReadAsync(
                    fileReader,
                    ContainedPath.Create(parent, programPath),
                    cancellationToken)
                .ConfigureAwait(false);
            return receipt switch
            {
                ProgramDefinitionRootFileReceiptSuccess success => ProgramInputResolutionResult.Success(project, new FileProgramDefinitionResolutionInput(success.Receipt)),
                ProgramDefinitionRootFileReceiptReadFailure { ReadResult: ProgramDefinitionFileReadOutsideBoundary } => ProgramInputResolutionResult.ValidationFailure(project, [new ProgramDiagnostic("program.referenceBoundary", null, "Program file resolves outside its reference boundary.")]),
                ProgramDefinitionRootFileReceiptReadFailure { ReadResult: ProgramDefinitionFileReadUnavailable unavailable } => ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument(unavailable.Message)),
                ProgramDefinitionRootFileReceiptReadFailure { ReadResult: ProgramDefinitionFileReadChangedDuringRead } => ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument("Program file changed while it was being read.")),
                ProgramDefinitionRootFileReceiptInvalidUtf8 invalidUtf8 => ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument($"Program file is not strict UTF-8. {invalidUtf8.Message}")),
                ProgramDefinitionRootFileReceiptInvalidParent => ProgramInputResolutionResult.Failure(ExecutionError.InvalidArgument("Program file has no reference parent directory.")),
                _ => throw new InvalidOperationException("Program file resolution result is not supported."),
            };
        }

        var stdin = await inputReader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return stdin.IsSuccess
            ? ProgramInputResolutionResult.Success(project, new StdinProgramDefinitionResolutionInput(stdin.Json!))
            : ProgramInputResolutionResult.Failure(stdin.Error!);
    }
}

internal sealed record ProgramInputResolutionResult (
    ProjectContext? Project,
    ProgramDefinitionResolutionInput? Input,
    ProgramDefinitionResolutionResult? ResolvedDefinition,
    IReadOnlyList<ProgramDiagnostic> Diagnostics,
    ExecutionError? Error)
{
    public bool IsSuccess => Project is not null && (Input is not null || ResolvedDefinition is not null) && Diagnostics.Count == 0 && Error is null;
    public bool HasValidationFailure => Diagnostics.Count != 0;
    public static ProgramInputResolutionResult Success (ProjectContext project, ProgramDefinitionResolutionInput input) => new(project, input, null, [], null);
    public static ProgramInputResolutionResult Success (ProjectContext project, ProgramDefinitionResolutionResult definition) => new(project, null, definition, [], null);
    public static ProgramInputResolutionResult ValidationFailure (ProjectContext project, IReadOnlyList<ProgramDiagnostic> diagnostics) => new(project, null, null, diagnostics, null);
    public static ProgramInputResolutionResult Failure (ExecutionError error) => new(null, null, null, [], error);
}
