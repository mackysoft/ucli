using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Maps config diagnostics to execution errors at application boundaries. </summary>
internal static class UcliConfigDiagnosticErrorMapper
{
    /// <summary> Converts a failed config-load result to one execution error. </summary>
    /// <param name="result"> The failed config-load result. </param>
    /// <param name="diagnosticFallbackMessage"> The message used when the result does not contain diagnostics or a structured error. </param>
    /// <returns> The mapped execution error. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="diagnosticFallbackMessage" /> is empty. </exception>
    public static ExecutionError ToExecutionError (
        UcliConfigLoadResult result,
        string diagnosticFallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ToExecutionError(result.Error, result.Diagnostics, diagnosticFallbackMessage);
    }

    /// <summary> Converts a failed config-save result to one execution error. </summary>
    /// <param name="result"> The failed config-save result. </param>
    /// <param name="diagnosticFallbackMessage"> The message used when the result does not contain diagnostics or a structured error. </param>
    /// <returns> The mapped execution error. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="result" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="diagnosticFallbackMessage" /> is empty. </exception>
    public static ExecutionError ToExecutionError (
        UcliConfigSaveResult result,
        string diagnosticFallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ToExecutionError(result.Error, result.Diagnostics, diagnosticFallbackMessage);
    }

    private static string FormatDiagnostic (UcliConfigDiagnostic diagnostic)
    {
        return string.IsNullOrWhiteSpace(diagnostic.SourcePath)
            ? diagnostic.Message
            : $"{diagnostic.Message} {diagnostic.SourcePath}";
    }

    private static ExecutionError ToInvalidArgument (
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string fallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackMessage);

        if (diagnostics.Count == 0)
        {
            return ExecutionError.InvalidArgument(fallbackMessage);
        }

        return ExecutionError.InvalidArgument(string.Join("; ", diagnostics.Select(FormatDiagnostic)));
    }

    private static ExecutionError ToExecutionError (
        ExecutionError? error,
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string diagnosticFallbackMessage)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticFallbackMessage);

        if (diagnostics.Count > 0)
        {
            return ToInvalidArgument(diagnostics, diagnosticFallbackMessage);
        }

        return error ?? ExecutionError.InvalidArgument(diagnosticFallbackMessage);
    }
}
