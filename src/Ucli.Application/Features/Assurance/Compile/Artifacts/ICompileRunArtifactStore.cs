using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;

/// <summary> Reads and writes compile run artifacts persisted under local uCLI storage. </summary>
internal interface ICompileRunArtifactStore : ICompileRunArtifactReader
{
    /// <summary> Writes completed compile artifacts for one run. </summary>
    ValueTask<ExecutionError?> WriteArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        string runId,
        IpcCompileSummary summary,
        CancellationToken cancellationToken = default);
}
