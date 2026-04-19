using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Shared.EnvironmentVariables;

namespace MackySoft.Ucli.UnityIntegration.Project;

/// <summary> Resolves project-path inputs using command, environment, and fallback precedence. </summary>
internal sealed class ProjectPathInputResolver : IProjectPathInputResolver
{
    private readonly IEnvironmentVariableReader environmentVariableReader;

    /// <summary> Initializes a new instance of the <see cref="ProjectPathInputResolver" /> class. </summary>
    /// <param name="environmentVariableReader"> The environment-variable reader dependency. </param>
    public ProjectPathInputResolver (IEnvironmentVariableReader environmentVariableReader)
    {
        this.environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
    }

    /// <inheritdoc />
    public string? Resolve (
        string? commandOptionProjectPath,
        string? fallbackProjectPath = null)
    {
        if (StringValueNormalizer.TryTrimToNonEmpty(commandOptionProjectPath, out var resolvedProjectPath))
        {
            return resolvedProjectPath;
        }

        var environmentProjectPath = environmentVariableReader.Get(UcliEnvironmentVariableNames.ProjectPath);
        if (StringValueNormalizer.TryTrimToNonEmpty(environmentProjectPath, out resolvedProjectPath))
        {
            return resolvedProjectPath;
        }

        if (StringValueNormalizer.TryTrimToNonEmpty(fallbackProjectPath, out resolvedProjectPath))
        {
            return resolvedProjectPath;
        }

        return null;
    }
}