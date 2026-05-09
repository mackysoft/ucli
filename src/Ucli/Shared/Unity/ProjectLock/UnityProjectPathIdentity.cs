using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Normalizes Unity project paths for lock ownership comparisons. </summary>
internal static class UnityProjectPathIdentity
{
    /// <summary> Attempts to normalize one project path for stable identity comparison. </summary>
    /// <param name="projectPath"> The project path value. </param>
    /// <param name="normalizedPath"> The normalized path when successful. </param>
    /// <param name="errorMessage"> The normalization error when unsuccessful. </param>
    /// <returns> <see langword="true" /> when path normalization succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryNormalize (
        string projectPath,
        out string normalizedPath,
        out string? errorMessage)
    {
        normalizedPath = string.Empty;
        errorMessage = null;

        try
        {
            normalizedPath = PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(projectPath);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

}
