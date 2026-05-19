namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines operation-phase literal values used by <c>execute</c> response payload contracts. </summary>
public static class IpcExecuteOperationPhaseNames
{
    /// <summary> Gets the phase literal for <c>validate</c>. </summary>
    public const string Validate = "validate";

    /// <summary> Gets the phase literal for <c>plan</c>. </summary>
    public const string Plan = "plan";

    /// <summary> Gets the phase literal for <c>call</c>. </summary>
    public const string Call = "call";

    /// <summary> Gets the phase literal for <c>skipped</c>. </summary>
    public const string Skipped = "skipped";
}
