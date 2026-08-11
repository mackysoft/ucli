using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary>Represents normalization of one required external project-path value.</summary>
internal sealed class ProjectPathNormalizationResult
{
    private ProjectPathNormalizationResult (AbsolutePath? projectPath, ExecutionError? error)
    {
        ProjectPath = projectPath;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(ProjectPath))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public AbsolutePath? ProjectPath { get; }

    public ExecutionError? Error { get; }

    public static ProjectPathNormalizationResult Success (AbsolutePath projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        return new ProjectPathNormalizationResult(projectPath, null);
    }

    public static ProjectPathNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ProjectPathNormalizationResult(null, error);
    }
}
