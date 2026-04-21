namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines literal values for <c>operationPolicy</c> in <c>.ucli/config.json</c>. </summary>
public static class OperationPolicyValues
{
    /// <summary> Gets the value that allows only safe operations. </summary>
    public const string Safe = "safe";

    /// <summary> Gets the value that allows safe and advanced operations. </summary>
    public const string Advanced = "advanced";

    /// <summary> Gets the value that allows safe, advanced, and dangerous operations. </summary>
    public const string Dangerous = "dangerous";
}