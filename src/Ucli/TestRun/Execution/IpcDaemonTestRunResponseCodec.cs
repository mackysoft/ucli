using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Decodes daemon <c>test.run</c> IPC responses into validated execution results. </summary>
internal static class IpcDaemonTestRunResponseCodec
{
    /// <summary> Tries to decode one daemon response into supported test-run exit code. </summary>
    /// <param name="response"> The daemon response envelope. </param>
    /// <param name="exitCode"> The decoded exit code when decoding succeeds. </param>
    /// <param name="errorMessage"> The decode error message when decoding fails. </param>
    /// <returns> <see langword="true" /> when decoding succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecode (
        IpcResponse response,
        out int exitCode,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal))
        {
            if (response.Errors.Count > 0)
            {
                var firstError = response.Errors[0];
                exitCode = default;
                errorMessage = $"Unity daemon test run failed with error code '{firstError.Code}'. {firstError.Message}";
                return false;
            }

            exitCode = default;
            errorMessage = $"Unity daemon test run failed with status '{response.Status}'.";
            return false;
        }

        if (response.Errors.Count > 0)
        {
            var firstError = response.Errors[0];
            exitCode = default;
            errorMessage = $"Unity daemon test run failed with error code '{firstError.Code}'. {firstError.Message}";
            return false;
        }

        if (!TryReadExitCode(response.Payload, out exitCode, out var readError))
        {
            errorMessage = $"Unity daemon test run payload is invalid. {readError}";
            return false;
        }

        if (exitCode != 0 && exitCode != 2)
        {
            errorMessage = $"Unity daemon test run returned unsupported exit code: {exitCode}.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary> Reads required <c>exitCode</c> from one daemon response payload. </summary>
    /// <param name="payload"> The daemon response payload. </param>
    /// <param name="exitCode"> The parsed exit code when reading succeeds. </param>
    /// <param name="error"> The read error message when reading fails. </param>
    /// <returns> <see langword="true" /> when payload contains valid integer <c>exitCode</c>; otherwise <see langword="false" />. </returns>
    private static bool TryReadExitCode (
        JsonElement payload,
        out int exitCode,
        out string? error)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            exitCode = default;
            error = "Response payload must be a JSON object.";
            return false;
        }

        if (!payload.TryGetProperty("exitCode", out var exitCodeElement))
        {
            exitCode = default;
            error = "Required property 'exitCode' is missing.";
            return false;
        }

        if (!exitCodeElement.TryGetInt32(out exitCode))
        {
            exitCode = default;
            error = "Property 'exitCode' must be an integer.";
            return false;
        }

        error = null;
        return true;
    }
}