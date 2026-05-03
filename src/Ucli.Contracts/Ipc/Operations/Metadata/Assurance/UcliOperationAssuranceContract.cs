namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Describes machine-readable assurance metadata for one primitive operation. </summary>
public sealed class UcliOperationAssuranceContract
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationAssuranceContract" /> class. </summary>
    public UcliOperationAssuranceContract ()
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationAssuranceContract" /> class. </summary>
    /// <param name="sideEffects"> The side-effect literals that can happen during <c>call</c>. </param>
    /// <param name="mayDirty"> Whether <c>call</c> can dirty Unity objects or project state. </param>
    /// <param name="mayPersist"> Whether <c>call</c> can persist data to project files. </param>
    /// <param name="touchedKinds"> The touched-resource kind literals that can be reported. </param>
    /// <param name="planMode"> The plan behavior literal. </param>
    public UcliOperationAssuranceContract (
        IReadOnlyList<string>? sideEffects,
        bool mayDirty,
        bool mayPersist,
        IReadOnlyList<string>? touchedKinds,
        string? planMode)
    {
        SideEffects = sideEffects;
        MayDirty = mayDirty;
        MayPersist = mayPersist;
        TouchedKinds = touchedKinds;
        PlanMode = planMode;
    }

    /// <summary> Initializes a new instance of the <see cref="UcliOperationAssuranceContract" /> class. </summary>
    /// <param name="sideEffects"> The side-effect enum values that can happen during <c>call</c>. </param>
    /// <param name="mayDirty"> Whether <c>call</c> can dirty Unity objects or project state. </param>
    /// <param name="mayPersist"> Whether <c>call</c> can persist data to project files. </param>
    /// <param name="touchedKinds"> The touched-resource kind literals that can be reported. </param>
    /// <param name="planMode"> The plan behavior enum value. </param>
    public UcliOperationAssuranceContract (
        IReadOnlyList<UcliOperationSideEffect>? sideEffects,
        bool mayDirty,
        bool mayPersist,
        IReadOnlyList<string>? touchedKinds,
        UcliOperationPlanMode planMode)
        : this(
            ConvertSideEffects(sideEffects),
            mayDirty,
            mayPersist,
            touchedKinds,
            UcliOperationPlanModeCodec.ToValue(planMode))
    {
    }

    /// <summary> Gets or sets side-effect literals that can happen during <c>call</c>. </summary>
    public IReadOnlyList<string>? SideEffects { get; set; }

    /// <summary> Gets or sets a value indicating whether <c>call</c> can dirty Unity objects or project state. </summary>
    public bool MayDirty { get; set; }

    /// <summary> Gets or sets a value indicating whether <c>call</c> can persist data to project files. </summary>
    public bool MayPersist { get; set; }

    /// <summary> Gets or sets touched-resource kind literals that can be reported. </summary>
    public IReadOnlyList<string>? TouchedKinds { get; set; }

    /// <summary> Gets or sets the plan behavior literal. </summary>
    public string? PlanMode { get; set; }

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
}
