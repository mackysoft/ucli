namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines high-level operation kinds exposed by the operation catalog. </summary>
public enum UcliOperationKind
{
    /// <summary> Represents read-only operations. </summary>
    Query = 0,

    /// <summary> Represents state-changing operations. </summary>
    Mutation = 1,
}