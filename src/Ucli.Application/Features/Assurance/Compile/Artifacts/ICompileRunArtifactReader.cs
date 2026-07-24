using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;

/// <summary> Reads compile run artifacts persisted by Unity. </summary>
internal interface ICompileRunArtifactReader
{
    /// <summary> Reads one compile summary artifact when available. </summary>
    ValueTask<CompileRunArtifactReadResult> ReadSummaryAsync (
        ResolvedUnityProjectContext unityProject,
        Guid runId,
        CancellationToken cancellationToken = default);

    /// <summary> Resolves the summary artifact path for one compile run. </summary>
    AbsolutePath ResolveSummaryPath (
        ResolvedUnityProjectContext unityProject,
        Guid runId);

    /// <summary> Resolves the diagnostics artifact path for one compile run. </summary>
    AbsolutePath ResolveDiagnosticsPath (
        ResolvedUnityProjectContext unityProject,
        Guid runId);
}
