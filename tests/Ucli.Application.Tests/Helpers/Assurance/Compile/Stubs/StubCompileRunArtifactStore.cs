using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubCompileRunArtifactStore : ICompileRunArtifactStore
{
    private readonly Queue<CompileRunArtifactReadResult> results;

    public StubCompileRunArtifactStore (params CompileRunArtifactReadResult[] results)
    {
        this.results = new Queue<CompileRunArtifactReadResult>(results);
    }

    public int ReadCount { get; private set; }

    public int WriteCount { get; private set; }

    public IpcCompileSummary? WrittenSummary { get; private set; }

    public AbsolutePath SummaryPath { get; } = AbsolutePath.Parse(
        Path.Combine(Path.GetTempPath(), "workspace", ".ucli", "local", "compile", "run-1", "summary.json"));

    public AbsolutePath DiagnosticsPath { get; } = AbsolutePath.Parse(
        Path.Combine(Path.GetTempPath(), "workspace", ".ucli", "local", "compile", "run-1", "diagnostics.json"));

    public ValueTask<CompileRunArtifactReadResult> ReadSummaryAsync (
        ResolvedUnityProjectContext unityProject,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCount++;
        return ValueTask.FromResult(results.Count == 0
            ? CompileRunArtifactReadResult.Missing()
            : results.Dequeue());
    }

    public ValueTask<ExecutionError?> WriteArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        Guid runId,
        IpcCompileSummary summary,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteCount++;
        WrittenSummary = summary;
        return ValueTask.FromResult<ExecutionError?>(null);
    }

    public AbsolutePath ResolveSummaryPath (
        ResolvedUnityProjectContext unityProject,
        Guid runId)
    {
        return SummaryPath;
    }

    public AbsolutePath ResolveDiagnosticsPath (
        ResolvedUnityProjectContext unityProject,
        Guid runId)
    {
        return DiagnosticsPath;
    }
}
