using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one established evidence entry in a verify claim. </summary>
internal abstract record VerifyEvidenceOutput
{
    protected VerifyEvidenceOutput (VerifyEvidenceKind kind)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported verify evidence kind.");
        }

        Kind = kind;
    }

    [JsonIgnore]
    public VerifyEvidenceKind Kind { get; }
}

internal abstract record VerifyInlineEvidenceOutput<TData> : VerifyEvidenceOutput
    where TData : class
{
    protected VerifyInlineEvidenceOutput (
        VerifyEvidenceKind kind,
        TData data)
        : base(kind)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public TData Data { get; }
}

internal abstract record VerifyReferencedEvidenceOutput : VerifyEvidenceOutput
{
    protected VerifyReferencedEvidenceOutput (
        VerifyEvidenceKind kind,
        AssuranceReportId evidenceRef)
        : base(kind)
    {
        EvidenceRef = evidenceRef ?? throw new ArgumentNullException(nameof(evidenceRef));
    }

    public AssuranceReportId EvidenceRef { get; }
}

internal abstract record VerifyReferencedInlineEvidenceOutput<TData>
    : VerifyReferencedEvidenceOutput
    where TData : class
{
    protected VerifyReferencedInlineEvidenceOutput (
        VerifyEvidenceKind kind,
        AssuranceReportId evidenceRef,
        TData data)
        : base(kind, evidenceRef)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public TData Data { get; }
}

internal sealed record VerifyScriptEvidenceOutput
    : VerifyReferencedInlineEvidenceOutput<CompileScriptCompilationOutput>
{
    private VerifyScriptEvidenceOutput (
        AssuranceReportId evidenceRef,
        CompileScriptCompilationOutput data)
        : base(VerifyEvidenceKind.ScriptCompilation, evidenceRef, data)
    {
    }

    public static VerifyScriptEvidenceOutput Create (CompileScriptEvidenceOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new VerifyScriptEvidenceOutput(source.EvidenceRef, source.Data);
    }
}

internal sealed record VerifyDomainReloadEvidenceOutput
    : VerifyInlineEvidenceOutput<CompileDomainReloadOutput>
{
    private VerifyDomainReloadEvidenceOutput (CompileDomainReloadOutput data)
        : base(VerifyEvidenceKind.DomainReload, data)
    {
    }

    public static VerifyDomainReloadEvidenceOutput Create (CompileDomainReloadEvidenceOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new VerifyDomainReloadEvidenceOutput(source.Data);
    }
}

internal sealed record VerifyReadyLifecycleEvidenceOutput
    : VerifyInlineEvidenceOutput<ReadyLifecycleOutput>
{
    private VerifyReadyLifecycleEvidenceOutput (ReadyLifecycleOutput data)
        : base(VerifyEvidenceKind.ReadyLifecycleSnapshot, data)
    {
    }

    public static VerifyReadyLifecycleEvidenceOutput Create (ReadyLifecycleEvidenceOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new VerifyReadyLifecycleEvidenceOutput(source.Data);
    }
}

internal sealed record VerifyCompileLifecycleEvidenceOutput
    : VerifyInlineEvidenceOutput<CompileLifecycleOutput>
{
    private VerifyCompileLifecycleEvidenceOutput (CompileLifecycleOutput data)
        : base(VerifyEvidenceKind.CompileLifecycleSnapshot, data)
    {
    }

    public static VerifyCompileLifecycleEvidenceOutput Create (CompileLifecycleEvidenceOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new VerifyCompileLifecycleEvidenceOutput(source.Data);
    }
}

internal sealed record VerifyReadinessEvidenceOutput
    : VerifyInlineEvidenceOutput<ReadyDecisionEvidenceData>
{
    private VerifyReadinessEvidenceOutput (ReadyDecisionEvidenceData data)
        : base(VerifyEvidenceKind.ReadinessDecision, data)
    {
    }

    public static VerifyReadinessEvidenceOutput Create (ReadyDecisionEvidenceOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new VerifyReadinessEvidenceOutput(source.Data);
    }
}

internal sealed record VerifyReadIndexEvidenceOutput
    : VerifyInlineEvidenceOutput<ReadyReadIndexOutput>
{
    private VerifyReadIndexEvidenceOutput (ReadyReadIndexOutput data)
        : base(VerifyEvidenceKind.ReadIndexSummary, data)
    {
    }

    public static VerifyReadIndexEvidenceOutput Create (ReadyReadIndexEvidenceOutput source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new VerifyReadIndexEvidenceOutput(source.Data);
    }
}

internal sealed record VerifyTestSummaryEvidenceOutput : VerifyReferencedEvidenceOutput
{
    public VerifyTestSummaryEvidenceOutput (AssuranceReportId evidenceRef)
        : base(VerifyEvidenceKind.TestSummary, evidenceRef)
    {
    }
}

internal sealed record VerifyFromResultMissingEvidenceOutput
    : VerifyInlineEvidenceOutput<VerifyMissingFromResultEvidenceData>
{
    public VerifyFromResultMissingEvidenceOutput ()
        : base(
            VerifyEvidenceKind.FromResultMissing,
            new VerifyMissingFromResultEvidenceData())
    {
    }
}

internal sealed record VerifyFromResultSummaryEvidenceOutput
    : VerifyInlineEvidenceOutput<VerifyObservedFromResultEvidenceData>
{
    public VerifyFromResultSummaryEvidenceOutput (
        string command,
        int operationResultCount,
        int changedCount,
        int touchedCount,
        int diagnosticCount,
        VerifyDiagnosticImpact diagnosticImpact)
        : base(
            VerifyEvidenceKind.FromResultSummary,
            new VerifyObservedFromResultEvidenceData(
                command,
                operationResultCount,
                changedCount,
                touchedCount,
                diagnosticCount,
                diagnosticImpact))
    {
    }
}

/// <summary> Represents the empty object carried when post-read input is absent. </summary>
internal sealed record VerifyMissingFromResultEvidenceData;

/// <summary> Represents the observed result summary supplied to post-read verification. </summary>
internal sealed record VerifyObservedFromResultEvidenceData (
    string Command,
    int OperationResultCount,
    int ChangedCount,
    int TouchedCount,
    int DiagnosticCount,
    VerifyDiagnosticImpact DiagnosticImpact);
