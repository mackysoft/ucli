namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Identifies one root Program input and its permitted request-reference root. </summary>
internal sealed record ProgramDefinitionResolutionInput (
    string Json,
    ProgramRootSource RootSource,
    string? RootPath,
    string? PresetId,
    string? ReferenceRootPath);

/// <summary> Identifies the origin of a Program root. </summary>
internal enum ProgramRootSource
{
    Stdin,
    File,
    Preset,
}
