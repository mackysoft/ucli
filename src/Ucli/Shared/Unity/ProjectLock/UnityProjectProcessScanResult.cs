namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents the result of scanning operating-system Unity processes for one project. </summary>
/// <param name="Matches"> The matching Unity processes when scan succeeds. </param>
/// <param name="ErrorMessage"> The scan failure message when scan failed. </param>
internal sealed record UnityProjectProcessScanResult (
    IReadOnlyList<UnityProjectProcessMatch> Matches,
    string? ErrorMessage)
{
    /// <summary> Gets whether process scan completed successfully. </summary>
    public bool IsSuccess => ErrorMessage is null;

    /// <summary> Creates a successful scan result. </summary>
    /// <param name="matches"> The matching Unity processes. </param>
    /// <returns> The scan result. </returns>
    public static UnityProjectProcessScanResult Success (IReadOnlyList<UnityProjectProcessMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        return new UnityProjectProcessScanResult(matches, null);
    }

    /// <summary> Creates a failed scan result. </summary>
    /// <param name="errorMessage"> The scan failure message. </param>
    /// <returns> The scan result. </returns>
    public static UnityProjectProcessScanResult Failure (string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new UnityProjectProcessScanResult([], errorMessage);
    }
}
