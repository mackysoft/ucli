using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Represents one Unity-version resolution result. </summary>
/// <param name="UnityVersion"> The resolved Unity version on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record UnityVersionResolutionResult (
    string? UnityVersion,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether resolution succeeded. </summary>
    public bool IsSuccess => !string.IsNullOrWhiteSpace(UnityVersion) && Error is null;

    /// <summary> Creates a successful Unity-version resolution result. </summary>
    /// <param name="unityVersion"> The resolved Unity version. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static UnityVersionResolutionResult Success (string unityVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityVersion);
        return new UnityVersionResolutionResult(unityVersion, null);
    }

    /// <summary> Creates a failed Unity-version resolution result. </summary>
    /// <param name="error"> The structured resolution error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityVersionResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UnityVersionResolutionResult(null, error);
    }
}
