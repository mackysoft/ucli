namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines shared JSON property names used by operation contract types. </summary>
public static class UcliOperationContractPropertyNames
{
    /// <summary> Gets the JSON property name used by polymorphic operation contracts. </summary>
    public const string Kind = "kind";

    /// <summary> Gets the JSON property name used by request-local alias references. </summary>
    public const string Alias = "var";
}
