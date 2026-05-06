namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents the result of building effective config values. </summary>
/// <param name="Config"> The effective config when build succeeds. </param>
/// <param name="Diagnostics"> The semantic diagnostics when build fails. </param>
internal sealed record UcliConfigBuildResult (
    UcliConfig? Config,
    IReadOnlyList<UcliConfigDiagnostic> Diagnostics)
{
    /// <summary> Gets a value indicating whether effective config build succeeded. </summary>
    public bool IsSuccess => Config is not null && Diagnostics.Count == 0;

    /// <summary> Creates a successful build result. </summary>
    /// <param name="config"> The effective config. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public static UcliConfigBuildResult Success (UcliConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new UcliConfigBuildResult(config, Array.Empty<UcliConfigDiagnostic>());
    }

    /// <summary> Creates a failed build result. </summary>
    /// <param name="diagnostics"> The semantic diagnostics. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="diagnostics" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="diagnostics" /> is empty. </exception>
    public static UcliConfigBuildResult Failure (IReadOnlyList<UcliConfigDiagnostic> diagnostics)
    {
        return new UcliConfigBuildResult(null, UcliConfigDiagnosticList.CopyForFailure(diagnostics));
    }
}
