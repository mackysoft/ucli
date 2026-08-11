using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary>Represents selection and normalization of the effective Unity project path.</summary>
internal sealed class ProjectPathInputResolutionResult
{
    private ProjectPathInputResolutionResult (
        ProjectPathCandidate? candidate,
        ExecutionError? error)
    {
        Candidate = candidate;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Candidate))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public ProjectPathCandidate? Candidate { get; }

    public ExecutionError? Error { get; }

    public static ProjectPathInputResolutionResult Success (ProjectPathCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new ProjectPathInputResolutionResult(candidate, null);
    }

    public static ProjectPathInputResolutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ProjectPathInputResolutionResult(null, error);
    }
}
