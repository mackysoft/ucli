namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Reads build profile JSON from host storage. </summary>
internal interface IBuildProfileFileReader
{
    /// <summary> Reads one build profile JSON document. </summary>
    ValueTask<BuildProfileFileReadResult> ReadAsync (
        string? profilePath,
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
