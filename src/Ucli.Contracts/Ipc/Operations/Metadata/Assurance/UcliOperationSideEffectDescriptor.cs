using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the contract facts attached to an operation side-effect value. </summary>
internal sealed class UcliOperationSideEffectDescriptor
{
    /// <summary> Initializes a new instance of the <see cref="UcliOperationSideEffectDescriptor" /> class. </summary>
    /// <param name="sideEffect"> The side-effect enum value. </param>
    /// <param name="minimumPolicy"> The minimum operation policy required by the side effect. </param>
    /// <param name="allowedForQueryOperation"> Whether a query operation can declare the side effect. </param>
    /// <param name="requiredAssuranceFacts"> The assurance facts required when this side effect is declared. </param>
    internal UcliOperationSideEffectDescriptor (
        UcliOperationSideEffect sideEffect,
        OperationPolicy minimumPolicy,
        bool allowedForQueryOperation,
        IReadOnlyList<UcliOperationSideEffectRequiredAssuranceFact> requiredAssuranceFacts)
    {
        SideEffect = sideEffect;
        Value = UcliOperationSideEffectCodec.ToValue(sideEffect);
        MinimumPolicy = minimumPolicy;
        AllowedForQueryOperation = allowedForQueryOperation;
        RequiredAssuranceFacts = Array.AsReadOnly(CopyRequiredAssuranceFacts(requiredAssuranceFacts));
    }

    /// <summary> Gets the side-effect enum value. </summary>
    public UcliOperationSideEffect SideEffect { get; }

    /// <summary> Gets the side-effect wire literal. </summary>
    public string Value { get; }

    /// <summary> Gets the minimum operation policy required by the side effect. </summary>
    public OperationPolicy MinimumPolicy { get; }

    /// <summary> Gets a value indicating whether a query operation can declare the side effect. </summary>
    public bool AllowedForQueryOperation { get; }

    /// <summary> Gets the assurance facts required when this side effect is declared. </summary>
    public IReadOnlyList<UcliOperationSideEffectRequiredAssuranceFact> RequiredAssuranceFacts { get; }

    private static UcliOperationSideEffectRequiredAssuranceFact[] CopyRequiredAssuranceFacts (
        IReadOnlyList<UcliOperationSideEffectRequiredAssuranceFact> requiredAssuranceFacts)
    {
        if (requiredAssuranceFacts == null)
        {
            throw new ArgumentNullException(nameof(requiredAssuranceFacts));
        }

        var copy = new UcliOperationSideEffectRequiredAssuranceFact[requiredAssuranceFacts.Count];
        for (var i = 0; i < requiredAssuranceFacts.Count; i++)
        {
            copy[i] = requiredAssuranceFacts[i] ?? throw new ArgumentException("Required assurance facts must not contain null.", nameof(requiredAssuranceFacts));
        }

        return copy;
    }
}
