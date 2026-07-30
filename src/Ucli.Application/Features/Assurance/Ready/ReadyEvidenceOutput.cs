using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one evidence entry in a ready claim. </summary>
internal abstract record ReadyEvidenceOutput
{
    protected ReadyEvidenceOutput (ReadyEvidenceKind kind)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Ready evidence kind must be defined.");
        }

        Kind = kind;
    }

    [JsonIgnore]
    public ReadyEvidenceKind Kind { get; }
}

internal abstract record ReadyInlineEvidenceOutput<TData> : ReadyEvidenceOutput
    where TData : class
{
    protected ReadyInlineEvidenceOutput (
        ReadyEvidenceKind kind,
        TData data)
        : base(kind)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public TData Data { get; }
}

internal sealed record ReadyLifecycleEvidenceOutput
    : ReadyInlineEvidenceOutput<ReadyLifecycleOutput>
{
    private ReadyLifecycleEvidenceOutput (ReadyLifecycleOutput data)
        : base(ReadyEvidenceKind.LifecycleSnapshot, data)
    {
    }

    public static ReadyLifecycleEvidenceOutput Create (ReadyLifecycleOutput data)
    {
        return new ReadyLifecycleEvidenceOutput(data);
    }
}

/// <summary> Represents the failed readiness decision serialized as claim evidence. </summary>
internal sealed record ReadyDecisionEvidenceData (
    UcliCode? Code,
    string? Message);

internal sealed record ReadyDecisionEvidenceOutput
    : ReadyInlineEvidenceOutput<ReadyDecisionEvidenceData>
{
    private ReadyDecisionEvidenceOutput (ReadyDecisionEvidenceData data)
        : base(ReadyEvidenceKind.ReadinessDecision, data)
    {
    }

    public static ReadyDecisionEvidenceOutput Create (ReadyDecisionEvidenceData data)
    {
        return new ReadyDecisionEvidenceOutput(data);
    }
}

internal sealed record ReadyReadIndexEvidenceOutput
    : ReadyInlineEvidenceOutput<ReadyReadIndexOutput>
{
    private ReadyReadIndexEvidenceOutput (ReadyReadIndexOutput data)
        : base(ReadyEvidenceKind.ReadIndexSummary, data)
    {
    }

    public static ReadyReadIndexEvidenceOutput Create (ReadyReadIndexOutput data)
    {
        return new ReadyReadIndexEvidenceOutput(data);
    }
}
