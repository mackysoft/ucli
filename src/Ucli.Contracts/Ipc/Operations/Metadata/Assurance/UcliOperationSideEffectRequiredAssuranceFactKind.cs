namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines assurance fact kinds required by side-effect descriptors. </summary>
public enum UcliOperationSideEffectRequiredAssuranceFactKind
{
    /// <summary> Requires <c>assurance.mayDirty=true</c>. </summary>
    MayDirtyTrue = 0,

    /// <summary> Requires <c>assurance.mayPersist=true</c>. </summary>
    MayPersistTrue = 1,

    /// <summary> Requires <c>assurance.touchedKinds</c> to include the descriptor fact value. </summary>
    TouchedKindIncludes = 2,
}
