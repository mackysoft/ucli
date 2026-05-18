namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines an assurance fact required by a side-effect descriptor. </summary>
public sealed class UcliOperationSideEffectRequiredAssuranceFact
{
    private UcliOperationSideEffectRequiredAssuranceFact (
        UcliOperationSideEffectRequiredAssuranceFactKind kind,
        string? value)
    {
        if (kind == UcliOperationSideEffectRequiredAssuranceFactKind.TouchedKindIncludes)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Touched kind fact value must not be empty.", nameof(value));
            }
        }
        else if (value != null)
        {
            throw new ArgumentException("Only touched kind facts can carry a value.", nameof(value));
        }

        Kind = kind;
        Value = value;
    }

    /// <summary> Gets the required assurance fact kind. </summary>
    public UcliOperationSideEffectRequiredAssuranceFactKind Kind { get; }

    /// <summary> Gets the required fact value when the fact kind carries one. </summary>
    public string? Value { get; }

    /// <summary> Creates a requirement for <c>assurance.mayDirty=true</c>. </summary>
    /// <returns> The required assurance fact. </returns>
    internal static UcliOperationSideEffectRequiredAssuranceFact MayDirtyTrue ()
    {
        return new UcliOperationSideEffectRequiredAssuranceFact(
            UcliOperationSideEffectRequiredAssuranceFactKind.MayDirtyTrue,
            value: null);
    }

    /// <summary> Creates a requirement for <c>assurance.mayPersist=true</c>. </summary>
    /// <returns> The required assurance fact. </returns>
    internal static UcliOperationSideEffectRequiredAssuranceFact MayPersistTrue ()
    {
        return new UcliOperationSideEffectRequiredAssuranceFact(
            UcliOperationSideEffectRequiredAssuranceFactKind.MayPersistTrue,
            value: null);
    }

    /// <summary> Creates a requirement for <c>assurance.touchedKinds</c> to include a touched kind. </summary>
    /// <param name="touchedKind"> The required touched kind literal. </param>
    /// <returns> The required assurance fact. </returns>
    internal static UcliOperationSideEffectRequiredAssuranceFact TouchedKindIncludes (string touchedKind)
    {
        return new UcliOperationSideEffectRequiredAssuranceFact(
            UcliOperationSideEffectRequiredAssuranceFactKind.TouchedKindIncludes,
            touchedKind);
    }
}
