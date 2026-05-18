using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

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
        IReadOnlyList<string> requiredTouchedKinds)
    {
        SideEffect = sideEffect;
        Value = UcliOperationSideEffectCodec.ToValue(sideEffect);
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

    /// <summary> Gets touched-resource kind literals required when this side effect is declared. </summary>
    public IReadOnlyList<string> RequiredTouchedKinds { get; }

    private static string[] CopyRequiredTouchedKinds (IReadOnlyList<string> requiredTouchedKinds)
    {
        if (requiredTouchedKinds == null)
        {
            throw new ArgumentNullException(nameof(requiredTouchedKinds));
        }

        var copy = new string[requiredTouchedKinds.Count];
        for (var i = 0; i < requiredTouchedKinds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(requiredTouchedKinds[i]))
            {
                throw new ArgumentException("Required touched kinds must not contain null, empty, or whitespace values.", nameof(requiredTouchedKinds));
            }

            copy[i] = requiredTouchedKinds[i];
        }

        return copy;
    }
}
