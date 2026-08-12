using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Programs.Presets;

/// <summary> Resolves project Program Presets through the normal Program definition resolver. </summary>
internal sealed class ProgramPresetCatalog : IProgramPresetCatalog
{
    private const string UnknownPresetCode = "program.presetUnknown";
    private const string InvalidPresetPathCode = "program.presetPathInvalid";
    private const string PresetReadFailedCode = "program.presetReadFailed";

    private readonly IProgramDefinitionFileReader fileReader;
    private readonly IProgramDefinitionResolver definitionResolver;

    /// <summary> Initializes a new catalog. </summary>
    public ProgramPresetCatalog (IProgramDefinitionFileReader fileReader, IProgramDefinitionResolver definitionResolver)
    {
        this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
        this.definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
    }

    public async ValueTask<ProgramPresetResolutionResult> ResolveAsync (
        string id,
        UcliConfig config,
        string configDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!config.ProgramPresets.TryGetValue(id, out var registration))
        {
            return new ProgramPresetResolutionResult(null, [new ProgramDiagnostic(UnknownPresetCode, null, $"Program Preset '{id}' is not registered.")]);
        }

        var configRoot = AbsolutePath.Parse(configDirectoryPath);
        var receiptResult = await ProgramDefinitionRootFileReceipt.ReadAsync(
                fileReader,
                ContainedPath.Create(configRoot, registration.ProgramPath),
                cancellationToken)
            .ConfigureAwait(false);
        if (receiptResult is ProgramDefinitionRootFileReceiptReadFailure { ReadResult: ProgramDefinitionFileReadOutsideBoundary })
        {
            return new ProgramPresetResolutionResult(null, [new ProgramDiagnostic(InvalidPresetPathCode, null, "Program Preset path resolves outside .ucli after symbolic-link resolution.")]);
        }

        if (receiptResult is not ProgramDefinitionRootFileReceiptSuccess receiptSuccess)
        {
            var message = CreateReceiptFailureMessage(receiptResult);
            return new ProgramPresetResolutionResult(null, [new ProgramDiagnostic(PresetReadFailedCode, null, message)]);
        }

        var definitionResult = await definitionResolver.ResolveAsync(new PresetProgramDefinitionResolutionInput(
            id,
            receiptSuccess.Receipt), cancellationToken).ConfigureAwait(false);
        return definitionResult.IsSuccess
            ? new ProgramPresetResolutionResult(new ProgramPresetResolution(id, registration.Description, definitionResult.Definition!), Array.Empty<ProgramDiagnostic>())
            : new ProgramPresetResolutionResult(null, definitionResult.Diagnostics);
    }

    private static string CreateReceiptFailureMessage (ProgramDefinitionRootFileReceiptResult result)
    {
        return result switch
        {
            ProgramDefinitionRootFileReceiptReadFailure { ReadResult: ProgramDefinitionFileReadUnavailable unavailable } => unavailable.Message,
            ProgramDefinitionRootFileReceiptReadFailure { ReadResult: ProgramDefinitionFileReadChangedDuringRead } => "Program Preset file changed while it was being read.",
            ProgramDefinitionRootFileReceiptInvalidUtf8 invalidUtf8 => $"Program Preset is not strict UTF-8. {invalidUtf8.Message}",
            ProgramDefinitionRootFileReceiptInvalidParent => "Program Preset file has no reference parent directory.",
            _ => throw new InvalidOperationException($"Unknown Program Preset receipt result: {result.GetType().Name}."),
        };
    }

    public async ValueTask<ProgramPresetListResult> ListAsync (
        UcliConfig config,
        string configDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var presets = new List<ProgramPresetResolution>();
        foreach (var id in config.ProgramPresets.Keys.OrderBy(static value => value, StringComparer.Ordinal))
        {
            var result = await ResolveAsync(id, config, configDirectoryPath, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return new ProgramPresetListResult(null, result.Diagnostics);
            }

            presets.Add(result.Preset!);
        }

        return new ProgramPresetListResult(presets, Array.Empty<ProgramDiagnostic>());
    }

}
