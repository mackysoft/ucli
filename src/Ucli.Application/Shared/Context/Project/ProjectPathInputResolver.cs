using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Resolves project-path inputs using command, environment, fallback, and current-directory precedence. </summary>
internal sealed class ProjectPathInputResolver : IProjectPathInputResolver
{
    private const string CurrentDirectoryProjectPath = ".";

    private readonly IEnvironmentVariableReader environmentVariableReader;

    /// <summary> Initializes a new instance of the <see cref="ProjectPathInputResolver" /> class. </summary>
    /// <param name="environmentVariableReader"> The environment-variable reader dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="environmentVariableReader" /> is <see langword="null" />. </exception>
    public ProjectPathInputResolver (IEnvironmentVariableReader environmentVariableReader)
    {
        this.environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
    }

    /// <inheritdoc />
    public ProjectPathCandidate Resolve (ProjectContextResolutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (StringValueNormalizer.TryTrimToNonEmpty(input.CommandOptionProjectPath, out var resolvedProjectPath))
        {
            return new ProjectPathCandidate(
                resolvedProjectPath,
                UnityProjectPathSource.CommandOption,
                "--projectPath");
        }

        var environmentProjectPath = environmentVariableReader.Get(UcliEnvironmentVariableNames.ProjectPath);
        if (StringValueNormalizer.TryTrimToNonEmpty(environmentProjectPath, out resolvedProjectPath))
        {
            return new ProjectPathCandidate(
                resolvedProjectPath,
                UnityProjectPathSource.EnvironmentVariable,
                UcliEnvironmentVariableNames.ProjectPath);
        }

        if (StringValueNormalizer.TryTrimToNonEmpty(input.FallbackProjectPath, out resolvedProjectPath))
        {
            return new ProjectPathCandidate(
                resolvedProjectPath,
                UnityProjectPathSource.Fallback,
                input.FallbackSourceLabel);
        }

        return new ProjectPathCandidate(
            CurrentDirectoryProjectPath,
            UnityProjectPathSource.CurrentDirectory);
    }
}
