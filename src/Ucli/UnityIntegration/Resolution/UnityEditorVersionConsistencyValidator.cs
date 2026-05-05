using System.Text.RegularExpressions;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Validates version consistency between Unity editor executable paths and target Unity versions. </summary>
internal sealed class UnityEditorVersionConsistencyValidator
{
    private static readonly Regex UnityVersionRegex = new(
        @"^\d+\.\d+\.\d+[abcfp]\d+(?:c\d+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary> Validates that one editor path contains the same version as the target Unity version. </summary>
    /// <param name="unityEditorPath"> The editor executable path. </param>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <returns> The validation result as editor-path resolution output. </returns>
    /// <exception cref="ArgumentException"> Thrown when one input value is <see langword="null" />, empty, or whitespace. </exception>
    public UnityEditorPathResolutionResult Validate (
        string unityEditorPath,
        string unityVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityEditorPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityVersion);

        if (!TryGetVersionFromPath(unityEditorPath, out var detectedVersion))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityEditorPath version cannot be determined from standard layout: {unityEditorPath}"));
        }

        if (!string.Equals(detectedVersion, unityVersion, StringComparison.Ordinal))
        {
            return UnityEditorPathResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"unityVersion '{unityVersion}' conflicts with unityEditorPath version '{detectedVersion}'."));
        }

        return UnityEditorPathResolutionResult.Success(unityEditorPath);
    }

    /// <summary> Tries to extract one Unity version value from one editor path. </summary>
    /// <param name="unityEditorPath"> The editor executable path. </param>
    /// <param name="detectedVersion"> The detected version value. </param>
    /// <returns> <see langword="true" /> when one version value is detected; otherwise <see langword="false" />. </returns>
    private static bool TryGetVersionFromPath (
        string unityEditorPath,
        out string detectedVersion)
    {
        detectedVersion = string.Empty;

        var normalizedPath = unityEditorPath.Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            if (!UnityVersionRegex.IsMatch(segments[index]))
            {
                continue;
            }

            var previousSegment = index > 0 ? segments[index - 1] : null;
            var nextSegment = index < segments.Length - 1 ? segments[index + 1] : null;
            if (string.Equals(previousSegment, "Editor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nextSegment, "Editor", StringComparison.OrdinalIgnoreCase))
            {
                detectedVersion = segments[index];
                return true;
            }
        }

        return false;
    }
}
