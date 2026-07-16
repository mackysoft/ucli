using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines the contract facts attached to an operation side-effect value. </summary>
internal sealed class UcliOperationSideEffectDescriptor
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationSideEffectDescriptor" /> class. </summary>
    /// <param name="sideEffect"> The side-effect enum value. </param>
    /// <param name="minimumPolicy"> The minimum operation policy required by the side effect. </param>
    /// <param name="derivesMayDirty"> Whether the side effect derives <c>assurance.mayDirty=true</c>. </param>
    /// <param name="derivesMayPersist"> Whether the side effect derives <c>assurance.mayPersist=true</c>. </param>
    /// <param name="allowedForQueryOperation"> Whether a query operation can declare the side effect. </param>
    /// <param name="requiredTouchedKinds"> The touched-resource kinds required when this side effect is declared. </param>
    internal UcliOperationSideEffectDescriptor (
        UcliOperationSideEffect sideEffect,
        OperationPolicy minimumPolicy,
        bool derivesMayDirty,
        bool derivesMayPersist,
        bool allowedForQueryOperation,
        IReadOnlyList<UcliTouchedResourceKind> requiredTouchedKinds)
    {
        SideEffect = sideEffect;
        Value = ContractLiteralCodec.ToValue(sideEffect);
        MinimumPolicy = minimumPolicy;
        DerivesMayDirty = derivesMayDirty;
        DerivesMayPersist = derivesMayPersist;
        AllowedForQueryOperation = allowedForQueryOperation;
        RequiredTouchedKinds = Array.AsReadOnly(CopyRequiredTouchedKinds(requiredTouchedKinds));
    }

    /// <summary> Gets the side-effect enum value. </summary>
    public UcliOperationSideEffect SideEffect { get; }

    /// <summary> Gets the side-effect wire literal. </summary>
    public string Value { get; }

    /// <summary> Gets the minimum operation policy required by the side effect. </summary>
    public OperationPolicy MinimumPolicy { get; }

    /// <summary> Gets a value indicating whether the side effect derives <c>assurance.mayDirty=true</c>. </summary>
    public bool DerivesMayDirty { get; }

    /// <summary> Gets a value indicating whether the side effect derives <c>assurance.mayPersist=true</c>. </summary>
    public bool DerivesMayPersist { get; }

    /// <summary> Gets a value indicating whether a query operation can declare the side effect. </summary>
    public bool AllowedForQueryOperation { get; }

    /// <summary> Gets touched-resource kinds required when this side effect is declared. </summary>
    public IReadOnlyList<UcliTouchedResourceKind> RequiredTouchedKinds { get; }

    private static UcliTouchedResourceKind[] CopyRequiredTouchedKinds (IReadOnlyList<UcliTouchedResourceKind> requiredTouchedKinds)
    {
        if (requiredTouchedKinds == null)
        {
            throw new ArgumentNullException(nameof(requiredTouchedKinds));
        }

        var copy = new UcliTouchedResourceKind[requiredTouchedKinds.Count];
        for (var i = 0; i < requiredTouchedKinds.Count; i++)
        {
            if (!ContractLiteralCodec.IsDefined(requiredTouchedKinds[i]))
            {
                throw new ArgumentException("Required touched kinds must contain only specified contract values.", nameof(requiredTouchedKinds));
            }

            copy[i] = requiredTouchedKinds[i];
        }

        return copy;
    }
}
