using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Describes machine-readable assurance metadata for one primitive operation. </summary>
[JsonConverter(typeof(UcliOperationAssuranceContractJsonConverter))]
public sealed class UcliOperationAssuranceContract
{
    /// <summary> Initializes validated assurance metadata from typed contract values. </summary>
    /// <param name="sideEffects"> The side effects that can happen during <c>call</c>. </param>
    /// <param name="touchedKinds"> The touched-resource kinds that can be reported. </param>
    /// <param name="planMode"> The plan behavior. </param>
    /// <param name="planSemantics"> The plan-phase semantic contract. </param>
    /// <param name="callSemantics"> The call-phase semantic contract. </param>
    /// <param name="touchedContract"> The touched-resource reporting contract. </param>
    /// <param name="readPostconditionContract"> The post-mutation read-surface contract. </param>
    /// <param name="failureSemantics"> The timeout, cancellation, and partial-apply contract. </param>
    /// <param name="dangerousNotes"> Notes that describe out-of-contract or dangerous areas. </param>
    /// <exception cref="ArgumentException"> Thrown when one value violates the assurance contract. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when a required collection is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a finite contract value is undefined. </exception>
    public UcliOperationAssuranceContract (
        IReadOnlyList<UcliOperationSideEffect> sideEffects,
        IReadOnlyList<UcliTouchedResourceKind> touchedKinds,
        UcliOperationPlanMode planMode,
        string planSemantics,
        string callSemantics,
        string touchedContract,
        string readPostconditionContract,
        string failureSemantics,
        IReadOnlyList<string> dangerousNotes)
    {
        if (sideEffects == null)
        {
            throw new ArgumentNullException(nameof(sideEffects));
        }
        if (touchedKinds == null)
        {
            throw new ArgumentNullException(nameof(touchedKinds));
        }
        if (dangerousNotes == null)
        {
            throw new ArgumentNullException(nameof(dangerousNotes));
        }

        if (!TextVocabulary.IsDefined(planMode))
        {
            throw new ArgumentOutOfRangeException(nameof(planMode), planMode, "Operation plan mode must be defined.");
        }

        TouchedKinds = CopyTouchedKinds(touchedKinds);
        SideEffects = CopySideEffects(sideEffects, TouchedKinds, out var mayDirty, out var mayPersist);
        if (mayPersist && TouchedKinds.Count == 0)
        {
            throw new ArgumentException("Persisting side effects require at least one touched-resource kind.", nameof(touchedKinds));
        }

        DangerousNotes = CopyRequiredTextValues(dangerousNotes, nameof(dangerousNotes));
        PlanMode = planMode;
        PlanSemantics = RequireText(planSemantics, nameof(planSemantics));
        CallSemantics = RequireText(callSemantics, nameof(callSemantics));
        TouchedContract = RequireText(touchedContract, nameof(touchedContract));
        ReadPostconditionContract = RequireText(readPostconditionContract, nameof(readPostconditionContract));
        FailureSemantics = RequireText(failureSemantics, nameof(failureSemantics));
        MayDirty = mayDirty;
        MayPersist = mayPersist;
    }

    /// <summary> Gets the side effects that can happen during <c>call</c>. </summary>
    public IReadOnlyList<UcliOperationSideEffect> SideEffects { get; }

    /// <summary> Gets whether <c>call</c> can dirty Unity objects or project state. </summary>
    public bool MayDirty { get; }

    /// <summary> Gets the broad persistence projection for Unity saves and direct filesystem writes. </summary>
    public bool MayPersist { get; }

    /// <summary> Gets touched-resource kinds that can be reported. </summary>
    public IReadOnlyList<UcliTouchedResourceKind> TouchedKinds { get; }

    /// <summary> Gets the plan behavior. </summary>
    public UcliOperationPlanMode PlanMode { get; }

    /// <summary> Gets the plan-phase semantic contract. </summary>
    public string PlanSemantics { get; }

    /// <summary> Gets the call-phase semantic contract. </summary>
    public string CallSemantics { get; }

    /// <summary> Gets the touched-resource reporting contract. </summary>
    public string TouchedContract { get; }

    /// <summary> Gets the post-mutation read-surface contract. </summary>
    public string ReadPostconditionContract { get; }

    /// <summary> Gets the timeout, cancellation, and partial-apply contract. </summary>
    public string FailureSemantics { get; }

    /// <summary> Gets notes that describe out-of-contract or dangerous areas. </summary>
    public IReadOnlyList<string> DangerousNotes { get; }

    private static IReadOnlyList<UcliOperationSideEffect> CopySideEffects (
        IReadOnlyList<UcliOperationSideEffect> sideEffects,
        IReadOnlyList<UcliTouchedResourceKind> touchedKinds,
        out bool mayDirty,
        out bool mayPersist)
    {
        mayDirty = false;
        mayPersist = false;
        if (sideEffects.Count == 0)
        {
            return Array.Empty<UcliOperationSideEffect>();
        }

        var copy = new UcliOperationSideEffect[sideEffects.Count];
        for (var index = 0; index < sideEffects.Count; index++)
        {
            var sideEffect = sideEffects[index];
            if (!TextVocabulary.IsDefined(sideEffect))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sideEffects),
                    sideEffect,
                    $"Side effect at index {index} must be defined.");
            }
            var descriptor = UcliOperationSideEffectDescriptors.GetDescriptor(sideEffect);
            if (Contains(copy, index, sideEffect))
            {
                throw new ArgumentException($"Side effect at index {index} is duplicated.", nameof(sideEffects));
            }

            for (var requiredIndex = 0; requiredIndex < descriptor.RequiredTouchedKinds.Count; requiredIndex++)
            {
                if (!touchedKinds.Contains(descriptor.RequiredTouchedKinds[requiredIndex]))
                {
                    throw new ArgumentException(
                        $"Side effect '{descriptor.Value}' requires touched-resource kind "
                        + $"'{TextVocabulary.GetText(descriptor.RequiredTouchedKinds[requiredIndex])}'.",
                        nameof(touchedKinds));
                }
            }

            copy[index] = sideEffect;
            mayDirty |= descriptor.DerivesMayDirty;
            mayPersist |= descriptor.DerivesMayPersist;
        }

        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<UcliTouchedResourceKind> CopyTouchedKinds (
        IReadOnlyList<UcliTouchedResourceKind> touchedKinds)
    {
        if (touchedKinds.Count == 0)
        {
            return Array.Empty<UcliTouchedResourceKind>();
        }

        var copy = new UcliTouchedResourceKind[touchedKinds.Count];
        for (var index = 0; index < touchedKinds.Count; index++)
        {
            var touchedKind = touchedKinds[index];
            if (!TextVocabulary.IsDefined(touchedKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(touchedKinds),
                    touchedKind,
                    $"Touched-resource kind at index {index} must be defined.");
            }
            if (Contains(copy, index, touchedKind))
            {
                throw new ArgumentException($"Touched-resource kind at index {index} is duplicated.", nameof(touchedKinds));
            }

            copy[index] = touchedKind;
        }

        return Array.AsReadOnly(copy);
    }

    private static bool Contains<T> (
        T[] values,
        int count,
        T expected)
        where T : struct, Enum
    {
        for (var index = 0; index < count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(values[index], expected))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> CopyRequiredTextValues (
        IReadOnlyList<string> values,
        string parameterName)
    {
        if (values.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copy[index] = RequireText(values[index], parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    private static string RequireText (
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty or whitespace.", parameterName);
        }

        return value;
    }
}
