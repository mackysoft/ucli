namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents compile evidence grouped under <c>payload.compile</c>. </summary>
internal sealed record CompileOutput
{
    /// <summary> Initializes the complete evidence projection for one compile Lifecycle Execution. </summary>
    /// <param name="refresh"> The asset refresh evidence. </param>
    /// <param name="scriptCompilation"> The script compilation evidence. </param>
    /// <param name="domainReload"> The domain reload evidence. </param>
    /// <param name="lifecycle"> The terminal Unity lifecycle evidence. </param>
    /// <exception cref="ArgumentNullException"> Thrown when an evidence section is <see langword="null" />. </exception>
    public CompileOutput (
        CompileRefreshOutput refresh,
        CompileScriptCompilationOutput scriptCompilation,
        CompileDomainReloadOutput domainReload,
        CompileLifecycleOutput lifecycle)
    {
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(scriptCompilation);
        ArgumentNullException.ThrowIfNull(domainReload);
        ArgumentNullException.ThrowIfNull(lifecycle);

        Refresh = refresh;
        ScriptCompilation = scriptCompilation;
        DomainReload = domainReload;
        Lifecycle = lifecycle;
    }

    public CompileRefreshOutput Refresh { get; }

    public CompileScriptCompilationOutput ScriptCompilation { get; }

    public CompileDomainReloadOutput DomainReload { get; }

    public CompileLifecycleOutput Lifecycle { get; }
}
