namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported reference target-kind constraint parameters. </summary>
public enum UcliOperationReferenceTargetKind
{
    /// <summary> No target kind parameter is specified. </summary>
    Unspecified = 0,

    /// <summary> Asset reference target. </summary>
    Asset = 1,

    /// <summary> Component reference target. </summary>
    Component = 2,

    /// <summary> GameObject reference target. </summary>
    GameObject = 3,
}
