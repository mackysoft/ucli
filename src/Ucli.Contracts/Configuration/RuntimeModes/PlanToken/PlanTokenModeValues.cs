namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines canonical string literals for plan-token mode values. </summary>
public static class PlanTokenModeValues
{
    /// <summary> Gets the mode value that allows execution without plan token. </summary>
    public const string Optional = "optional";

    /// <summary> Gets the mode value that requires plan token for execution. </summary>
    public const string Required = "required";
}