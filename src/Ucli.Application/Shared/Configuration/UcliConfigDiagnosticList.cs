namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Provides shared diagnostic-list contracts for config result types. </summary>
internal static class UcliConfigDiagnosticList
{
    /// <summary> Copies diagnostics used to represent a failed result. </summary>
    /// <param name="diagnostics"> The diagnostics to copy. </param>
    /// <returns> The copied diagnostics. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="diagnostics" /> is empty. </exception>
    public static UcliConfigDiagnostic[] CopyForFailure (IReadOnlyList<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Count == 0)
        {
            throw new ArgumentException("Failure diagnostics must not be empty.", nameof(diagnostics));
        }

        return diagnostics.ToArray();
    }
}
