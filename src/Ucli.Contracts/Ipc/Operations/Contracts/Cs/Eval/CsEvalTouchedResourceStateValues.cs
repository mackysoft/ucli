namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines C# eval touched resource state literals. </summary>
public static class CsEvalTouchedResourceStateValues
{
    /// <summary> Gets the state used when touched resources were not declared. </summary>
    public const string Unknown = "unknown";

    /// <summary> Gets the state used when no resources were touched. </summary>
    public const string None = "none";

    /// <summary> Gets the state used when touched resources were declared. </summary>
    public const string Declared = "declared";
}
