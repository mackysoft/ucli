namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents compile evidence grouped under <c>payload.compile</c>. </summary>
internal sealed record CompileOutput
{
    /// <summary> Initializes the complete evidence projection for one identified compile run. </summary>
    /// <param name="runId"> The non-empty compile run identifier. </param>
    /// <param name="refresh"> The asset refresh evidence. </param>
    /// <param name="scriptCompilation"> The script compilation evidence. </param>
    /// <param name="domainReload"> The domain reload evidence. </param>
    /// <param name="lifecycle"> The terminal Unity lifecycle evidence. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when an evidence section is <see langword="null" />. </exception>
    public CompileOutput (
        Guid runId,
        CompileRefreshOutput refresh,
        CompileScriptCompilationOutput scriptCompilation,
        CompileDomainReloadOutput domainReload,
        CompileLifecycleOutput lifecycle)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(scriptCompilation);
        ArgumentNullException.ThrowIfNull(domainReload);
        ArgumentNullException.ThrowIfNull(lifecycle);

        RunId = runId;
        Refresh = refresh;
        ScriptCompilation = scriptCompilation;
        DomainReload = domainReload;
        Lifecycle = lifecycle;
    }

    /// <summary> Gets the non-empty compile run identifier. </summary>
    public Guid RunId { get; }

    public CompileRefreshOutput Refresh { get; }

    public CompileScriptCompilationOutput ScriptCompilation { get; }

    public CompileDomainReloadOutput DomainReload { get; }

    public CompileLifecycleOutput Lifecycle { get; }
}
