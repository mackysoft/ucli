namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes machine-readable assurance metadata for one primitive operation. </summary>
public sealed class UcliOperationAssuranceContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationAssuranceContract" /> class. </summary>
    public UcliOperationAssuranceContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationAssuranceContract" /> class. </summary>
    /// <param name="sideEffects"> The side-effect literals that can happen during <c>call</c>. </param>
    /// <param name="touchedKinds"> The touched-resource kind literals that can be reported. </param>
    /// <param name="planMode"> The plan behavior literal. </param>
    /// <param name="planSemantics"> The plan-phase semantic contract. </param>
    /// <param name="callSemantics"> The call-phase semantic contract. </param>
    /// <param name="touchedContract"> The touched-resource reporting contract. </param>
    /// <param name="readPostconditionContract"> The post-mutation read-surface contract. </param>
    /// <param name="failureSemantics"> The timeout, cancellation, and partial-apply contract. </param>
    /// <param name="dangerousNotes"> Notes that describe out-of-contract or dangerous areas. </param>
    public UcliOperationAssuranceContract (
        IReadOnlyList<string>? sideEffects,
        IReadOnlyList<string>? touchedKinds,
        string? planMode,
        string? planSemantics,
        string? callSemantics,
        string? touchedContract,
        string? readPostconditionContract,
        string? failureSemantics,
        IReadOnlyList<string>? dangerousNotes)
    {
        SideEffects = sideEffects;
        ApplyDerivedPersistenceProjection(sideEffects);
        TouchedKinds = touchedKinds;
        PlanMode = planMode;
        PlanSemantics = planSemantics;
        CallSemantics = callSemantics;
        TouchedContract = touchedContract;
        ReadPostconditionContract = readPostconditionContract;
        FailureSemantics = failureSemantics;
        DangerousNotes = dangerousNotes;
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationAssuranceContract" /> class. </summary>
    /// <param name="sideEffects"> The side-effect enum values that can happen during <c>call</c>. </param>
    /// <param name="touchedKinds"> The touched-resource kind literals that can be reported. </param>
    /// <param name="planMode"> The plan behavior enum value. </param>
    /// <param name="planSemantics"> The plan-phase semantic contract. </param>
    /// <param name="callSemantics"> The call-phase semantic contract. </param>
    /// <param name="touchedContract"> The touched-resource reporting contract. </param>
    /// <param name="readPostconditionContract"> The post-mutation read-surface contract. </param>
    /// <param name="failureSemantics"> The timeout, cancellation, and partial-apply contract. </param>
    /// <param name="dangerousNotes"> Notes that describe out-of-contract or dangerous areas. </param>
    public UcliOperationAssuranceContract (
        IReadOnlyList<UcliOperationSideEffect>? sideEffects,
        IReadOnlyList<string>? touchedKinds,
        UcliOperationPlanMode planMode,
        string? planSemantics,
        string? callSemantics,
        string? touchedContract,
        string? readPostconditionContract,
        string? failureSemantics,
        IReadOnlyList<string>? dangerousNotes)
        : this(
            ConvertSideEffects(sideEffects),
            touchedKinds,
            UcliOperationPlanModeCodec.ToValue(planMode),
            planSemantics,
            callSemantics,
            touchedContract,
            readPostconditionContract,
            failureSemantics,
            dangerousNotes)
    {
    }

    /// <summary> Gets or sets side-effect literals that can happen during <c>call</c>. </summary>
    public IReadOnlyList<string>? SideEffects { get; set; }

    /// <summary> Gets or sets the derived projection indicating whether <c>call</c> can dirty Unity objects or project state. </summary>
    public bool MayDirty { get; set; }

    /// <summary> Gets or sets the derived broad persistence projection for Unity saves and direct filesystem writes. </summary>
    public bool MayPersist { get; set; }

    /// <summary> Gets or sets touched-resource kind literals that can be reported. </summary>
    public IReadOnlyList<string>? TouchedKinds { get; set; }

    /// <summary> Gets or sets the plan behavior literal. </summary>
    public string? PlanMode { get; set; }

    /// <summary> Gets or sets the plan-phase semantic contract. </summary>
    public string? PlanSemantics { get; set; }

    /// <summary> Gets or sets the call-phase semantic contract. </summary>
    public string? CallSemantics { get; set; }

    /// <summary> Gets or sets the touched-resource reporting contract. </summary>
    public string? TouchedContract { get; set; }

    /// <summary> Gets or sets the post-mutation read-surface contract. </summary>
    public string? ReadPostconditionContract { get; set; }

    /// <summary> Gets or sets the timeout, cancellation, and partial-apply contract. </summary>
    public string? FailureSemantics { get; set; }

    /// <summary> Gets or sets notes that describe out-of-contract or dangerous areas. </summary>
    public IReadOnlyList<string>? DangerousNotes { get; set; }

    private static IReadOnlyList<string>? ConvertSideEffects (IReadOnlyList<UcliOperationSideEffect>? sideEffects)
    {
        if (sideEffects == null)
        {
            return null;
        }

        var values = new string[sideEffects.Count];
        for (var i = 0; i < sideEffects.Count; i++)
        {
            values[i] = UcliOperationSideEffectCodec.ToValue(sideEffects[i]);
        }

        return values;
    }

    private void ApplyDerivedPersistenceProjection (IReadOnlyList<string>? sideEffects)
    {
        if (UcliOperationSideEffectDescriptors.TryDeriveAssuranceProjection(sideEffects, out var mayDirty, out var mayPersist))
        {
            MayDirty = mayDirty;
            MayPersist = mayPersist;
            return;
        }

        MayDirty = false;
        MayPersist = false;
    }
}
