using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Resolves project-path inputs using command, environment, fallback, and current-directory precedence. </summary>
internal sealed class ProjectPathInputResolver : IProjectPathInputResolver
{
    private readonly IEnvironmentVariableReader environmentVariableReader;

    /// <summary> Initializes a new instance of the <see cref="ProjectPathInputResolver" /> class. </summary>
    /// <param name="environmentVariableReader"> The environment-variable reader dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="environmentVariableReader" /> is <see langword="null" />. </exception>
    public ProjectPathInputResolver (IEnvironmentVariableReader environmentVariableReader)
    {
        this.environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
    }

    /// <inheritdoc />
    public ProjectPathInputResolutionResult Resolve (ProjectContextResolutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.CommandOptionProjectPath is not null)
        {
            return Success(input.CommandOptionProjectPath, UnityProjectPathSource.CommandOption, "--projectPath");
        }

        var environmentProjectPath = environmentVariableReader.Get(UcliEnvironmentVariableNames.ProjectPath);
        if (environmentProjectPath is not null)
        {
            var normalizationResult = ProjectPathNormalizer.Normalize(
                environmentProjectPath,
                UcliEnvironmentVariableNames.ProjectPath);
            if (!normalizationResult.IsSuccess)
            {
                return ProjectPathInputResolutionResult.Failure(normalizationResult.Error);
            }

            return Success(
                normalizationResult.ProjectPath,
                UnityProjectPathSource.EnvironmentVariable,
                UcliEnvironmentVariableNames.ProjectPath);
        }

        if (input.FallbackProjectPath is not null)
        {
            return Success(
                input.FallbackProjectPath,
                UnityProjectPathSource.Fallback,
                input.FallbackSourceLabel);
        }

        return Success(
            AbsolutePath.Parse(Environment.CurrentDirectory),
            UnityProjectPathSource.CurrentDirectory);
    }

    private static ProjectPathInputResolutionResult Success (
        AbsolutePath projectPath,
        UnityProjectPathSource source,
        string? sourceLabel = null) =>
        ProjectPathInputResolutionResult.Success(new ProjectPathCandidate(projectPath, source, sourceLabel));
}
