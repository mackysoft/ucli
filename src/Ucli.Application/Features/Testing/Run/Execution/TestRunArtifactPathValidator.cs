using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Validates test-run artifact output paths before Unity process arguments are built. </summary>
internal static class TestRunArtifactPathValidator
{
    /// <summary> Tries to validate required output artifact file paths. </summary>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when required artifact paths are valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateOutputPaths (
        ArtifactPaths artifactPaths,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(artifactPaths);

        if (!Path.IsPathFullyQualified(artifactPaths.ResultsXmlPath))
        {
            errorMessage = $"results.xml path must be absolute: {artifactPaths.ResultsXmlPath}";
            return false;
        }

        if (!Path.IsPathFullyQualified(artifactPaths.EditorLogPath))
        {
            errorMessage = $"editor.log path must be absolute: {artifactPaths.EditorLogPath}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(artifactPaths.ResultsXmlPath)))
        {
            errorMessage = $"results.xml directory path could not be resolved: {artifactPaths.ResultsXmlPath}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(artifactPaths.EditorLogPath)))
        {
            errorMessage = $"editor.log directory path could not be resolved: {artifactPaths.EditorLogPath}";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
