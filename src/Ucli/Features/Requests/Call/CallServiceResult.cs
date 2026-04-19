using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Requests.Call;

/// <summary> Represents one normalized <c>call</c> service result. </summary>
/// <param name="Output"> The output payload when available. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="Errors"> The machine-readable error list. </param>
/// <param name="ExitCode"> The CLI exit code associated with this result. </param>
internal sealed record CallServiceResult (
    CallExecutionOutput? Output,
    string Message,
    IReadOnlyList<IpcError> Errors,
    int ExitCode)
{
    /// <summary> Gets a value indicating whether the service execution succeeded. </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static CallServiceResult Success (
        CallExecutionOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new CallServiceResult(output, message, [], 0);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errors"> The machine-readable failure errors. </param>
    /// <param name="exitCode"> The associated CLI exit code. </param>
    /// <param name="output"> The available output payload. </param>
    /// <returns> The failed result. </returns>
    public static CallServiceResult Failure (
        string message,
        IReadOnlyList<IpcError> errors,
        int exitCode,
        CallExecutionOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new CallServiceResult(output, message, errors, exitCode);
    }
}