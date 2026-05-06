namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Provides shared diagnostic-list contracts for config result types. </summary>
internal static class UcliConfigDiagnosticList
{
    /// <summary> Gets the maximum number of detailed diagnostics retained for one config failure. </summary>
    public const int MaxDetailedDiagnostics = 50;

    /// <summary> Gets the diagnostic code used when additional diagnostics were omitted. </summary>
    public const string OmittedDiagnosticsCode = "config.diagnostics.omitted";

    /// <summary> Gets the message used when additional diagnostics were omitted. </summary>
    public const string OmittedDiagnosticsMessage = "Additional config diagnostics were omitted.";

    /// <summary> Adds one diagnostic while enforcing the shared config diagnostic count limit. </summary>
    /// <param name="diagnostics"> The destination diagnostic list. </param>
    /// <param name="diagnostic"> The diagnostic to add. </param>
    /// <returns> <see langword="true" /> when more detailed diagnostics may be added; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> or <paramref name="diagnostic" /> is <see langword="null" />. </exception>
    public static bool Add (
        List<UcliConfigDiagnostic> diagnostics,
        UcliConfigDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(diagnostic);

        if (diagnostics.Count < MaxDetailedDiagnostics)
        {
            diagnostics.Add(diagnostic);
            return true;
        }

        if (diagnostics.Count == MaxDetailedDiagnostics)
        {
            diagnostics.Add(UcliConfigDiagnostic.Create(
                OmittedDiagnosticsCode,
                propertyPath: null,
                diagnostic.SourcePath,
                OmittedDiagnosticsMessage));
        }

        return false;
    }

    /// <summary> Gets whether the detailed diagnostic limit has already been reached. </summary>
    /// <param name="diagnostics"> The diagnostic list to inspect. </param>
    /// <returns> <see langword="true" /> when no more detailed diagnostics should be collected; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> is <see langword="null" />. </exception>
    public static bool HasReachedLimit (IReadOnlyCollection<UcliConfigDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return diagnostics.Count > MaxDetailedDiagnostics;
    }

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

        var copiedDiagnostics = new List<UcliConfigDiagnostic>(Math.Min(
            diagnostics.Count,
            MaxDetailedDiagnostics + 1));

        foreach (var diagnostic in diagnostics)
        {
            if (!Add(copiedDiagnostics, diagnostic))
            {
                break;
            }
        }

        return copiedDiagnostics.ToArray();
    }
}
