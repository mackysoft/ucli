namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents compile evidence grouped under <c>payload.compile</c>. </summary>
internal sealed record CompileOutput (
    string RunId,
    CompileRefreshOutput Refresh,
    CompileScriptCompilationOutput ScriptCompilation,
    CompileDomainReloadOutput DomainReload,
    CompileLifecycleOutput Lifecycle);
