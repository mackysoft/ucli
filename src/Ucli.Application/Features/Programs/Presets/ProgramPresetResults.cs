using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Resolution;

namespace MackySoft.Ucli.Application.Features.Programs.Presets;

/// <summary> Represents one resolved Program Preset. </summary>
internal sealed record ProgramPresetResolution (
    string Id,
    string Description,
    ResolvedProgramDefinition Definition);

/// <summary> Represents Program Preset resolution. </summary>
internal sealed record ProgramPresetResolutionResult (
    ProgramPresetResolution? Preset,
    IReadOnlyList<ProgramDiagnostic> Diagnostics)
{
    /// <summary> Gets whether resolution succeeded. </summary>
    public bool IsSuccess => Preset is not null && Diagnostics.Count == 0;
}

/// <summary> Represents complete Program Preset list resolution. </summary>
internal sealed record ProgramPresetListResult (
    IReadOnlyList<ProgramPresetResolution>? Presets,
    IReadOnlyList<ProgramDiagnostic> Diagnostics)
{
    /// <summary> Gets whether every preset resolved. </summary>
    public bool IsSuccess => Presets is not null && Diagnostics.Count == 0;
}
