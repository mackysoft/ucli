namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents one selected but not yet normalized Unity project path candidate. </summary>
/// <param name="Path"> The selected path candidate. </param>
/// <param name="Source"> The input source that provided <paramref name="Path" />. </param>
/// <param name="SourceLabel"> The optional source label used when the source has a command-specific identity. </param>
internal sealed record ProjectPathCandidate (
    string Path,
    UnityProjectPathSource Source,
    string? SourceLabel = null);
