using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents compile evidence grouped under <c>payload.compile</c>. </summary>
internal sealed record CompileOutput (
    string RunId,
    IpcCompileSummary.RefreshEvidence Refresh,
    IpcCompileSummary.ScriptCompilationEvidence ScriptCompilation,
    IpcCompileSummary.DomainReloadEvidence DomainReload,
    IpcCompileSummary.LifecycleEvidence Lifecycle);
