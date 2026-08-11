using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary>Normalizes external project-path text into the guarded absolute-path contract.</summary>
internal static class ProjectPathNormalizer
{
    public static ProjectPathNormalizationResult Normalize (
        string value,
        string sourceLabel)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);

        var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
        if (AbsolutePath.TryResolve(currentDirectory, value, out var projectPath, out var failure))
        {
            return ProjectPathNormalizationResult.Success(projectPath);
        }

        return ProjectPathNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"UnityProject path from {sourceLabel} is invalid. {failure.Message}",
            ProjectContextErrorCodes.ProjectPathInvalidFormat));
    }
}
