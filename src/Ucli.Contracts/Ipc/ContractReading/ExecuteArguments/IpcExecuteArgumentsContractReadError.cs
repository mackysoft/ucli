namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Represents a strict request-contract diagnostic.</summary>
/// <param name="Message">The diagnostic message, or an empty string when no error occurred.</param>
/// <param name="StepIndex">The zero-based step index, or <c>-1</c> for a root diagnostic.</param>
internal readonly record struct IpcExecuteArgumentsContractReadError (
    string Message,
    int StepIndex)
{
    public static IpcExecuteArgumentsContractReadError None => new(string.Empty, -1);

    public static IpcExecuteArgumentsContractReadError ContractViolation (
        string message,
        int stepIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("The diagnostic message must not be empty.", nameof(message));
        }

        return new IpcExecuteArgumentsContractReadError(message, stepIndex);
    }
}
