namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines literal values for operation-kind contract fields. </summary>
public static class UcliOperationKindValues
{
    /// <summary> Gets the value that represents read-only operations. </summary>
    public const string Query = "query";

    /// <summary> Gets the value that represents state-changing operations. </summary>
    public const string Mutation = "mutation";
}