using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Programs.Presets;

/// <summary> Resolves the single Program Preset namespace before normal Program resolution. </summary>
internal interface IProgramPresetCatalog
{
    /// <summary> Resolves one project-provided Program Preset. </summary>
    ValueTask<ProgramPresetResolutionResult> ResolveAsync (
        string id,
        UcliConfig config,
        string configDirectoryPath,
        CancellationToken cancellationToken = default);

    /// <summary> Resolves all project-provided Program Presets in ordinal ID order. </summary>
    ValueTask<ProgramPresetListResult> ListAsync (
        UcliConfig config,
        string configDirectoryPath,
        CancellationToken cancellationToken = default);
}
