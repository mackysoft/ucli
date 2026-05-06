using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Maps config diagnostics to execution errors at application boundaries. </summary>
internal static class UcliConfigDiagnosticErrorMapper
{
    /// <summary> Converts config diagnostics to one invalid-argument execution error. </summary>
    /// <param name="diagnostics"> The diagnostics to describe. </param>
    /// <param name="fallbackMessage"> The message used when <paramref name="diagnostics" /> is empty. </param>
    /// <returns> The mapped execution error. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="fallbackMessage" /> is empty. </exception>
    public static ExecutionError ToInvalidArgument (
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

    private static string FormatDiagnostic (UcliConfigDiagnostic diagnostic)
    {
        return string.IsNullOrWhiteSpace(diagnostic.SourcePath)
            ? diagnostic.Message
            : $"{diagnostic.Message} {diagnostic.SourcePath}";
    }
}
