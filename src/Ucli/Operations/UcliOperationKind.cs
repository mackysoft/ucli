namespace MackySoft.Ucli.Operations;

/// <summary> Defines high-level operation kinds exposed by the operation catalog. </summary>
internal enum UcliOperationKind
{
    /// <summary> Represents read-only operations. </summary>
    Query = 0,

    /// <summary> Represents state-changing operations. </summary>
    Mutation = 1,
}