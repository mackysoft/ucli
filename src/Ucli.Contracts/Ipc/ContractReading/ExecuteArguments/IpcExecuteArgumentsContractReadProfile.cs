namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Defines strictness options used when reading <c>execute</c> request arguments. </summary>
internal readonly record struct IpcExecuteArgumentsContractReadProfile (
    bool RequireProtocolVersion,
    bool RequireSteps,
    bool RequireStepObject,
    bool RejectDuplicatedStepId)
{
    /// <summary> Gets profile values used by strict <c>execute</c> arguments normalization. </summary>
    public static IpcExecuteArgumentsContractReadProfile StrictExecute => new(
        RequireProtocolVersion: true,
        RequireSteps: true,
        RequireStepObject: true,
        RejectDuplicatedStepId: true);

    /// <summary> Gets profile values used by permissive preflight parsing. </summary>
    public static IpcExecuteArgumentsContractReadProfile PermissivePreflight => new(
        RequireProtocolVersion: false,
        RequireSteps: false,
        RequireStepObject: false,
        RejectDuplicatedStepId: false);
}
