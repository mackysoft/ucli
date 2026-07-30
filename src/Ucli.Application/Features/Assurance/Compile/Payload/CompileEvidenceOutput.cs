using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents one established evidence entry in a compile assurance claim. </summary>
internal abstract record CompileEvidenceOutput
{
    protected CompileEvidenceOutput (CompileEvidenceKind kind)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported compile evidence kind.");
        }

        Kind = kind;
    }

    [JsonIgnore]
    public CompileEvidenceKind Kind { get; }
}

internal abstract record CompileInlineEvidenceOutput<TData> : CompileEvidenceOutput
    where TData : class
{
    protected CompileInlineEvidenceOutput (
        CompileEvidenceKind kind,
        TData data)
        : base(kind)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public TData Data { get; }
}

internal abstract record CompileReferencedInlineEvidenceOutput : CompileEvidenceOutput
{
    protected CompileReferencedInlineEvidenceOutput (
        CompileEvidenceKind kind,
        AssuranceReportId evidenceRef)
        : base(kind)
    {
        EvidenceRef = evidenceRef ?? throw new ArgumentNullException(nameof(evidenceRef));
    }

    public AssuranceReportId EvidenceRef { get; }
}

internal abstract record CompileReferencedInlineEvidenceOutput<TData>
    : CompileReferencedInlineEvidenceOutput
    where TData : class
{
    protected CompileReferencedInlineEvidenceOutput (
        CompileEvidenceKind kind,
        AssuranceReportId evidenceRef,
        TData data)
        : base(kind, evidenceRef)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public TData Data { get; }
}

internal sealed record CompileScriptEvidenceOutput
    : CompileReferencedInlineEvidenceOutput<CompileScriptCompilationOutput>
{
    private CompileScriptEvidenceOutput (
        AssuranceReportId evidenceRef,
        CompileScriptCompilationOutput data)
        : base(CompileEvidenceKind.ScriptCompilation, evidenceRef, data)
    {
    }

    public static CompileScriptEvidenceOutput Create (
        AssuranceReportId evidenceRef,
        CompileScriptCompilationOutput data)
    {
        return new CompileScriptEvidenceOutput(evidenceRef, data);
    }
}

internal sealed record CompileDomainReloadEvidenceOutput
    : CompileInlineEvidenceOutput<CompileDomainReloadOutput>
{
    private CompileDomainReloadEvidenceOutput (CompileDomainReloadOutput data)
        : base(CompileEvidenceKind.DomainReload, data)
    {
    }

    public static CompileDomainReloadEvidenceOutput Create (CompileDomainReloadOutput data)
    {
        return new CompileDomainReloadEvidenceOutput(data);
    }
}

internal sealed record CompileLifecycleEvidenceOutput
    : CompileInlineEvidenceOutput<CompileLifecycleOutput>
{
    private CompileLifecycleEvidenceOutput (CompileLifecycleOutput data)
        : base(CompileEvidenceKind.LifecycleSnapshot, data)
    {
    }

    public static CompileLifecycleEvidenceOutput Create (CompileLifecycleOutput data)
    {
        return new CompileLifecycleEvidenceOutput(data);
    }
}
