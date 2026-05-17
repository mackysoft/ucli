using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents the application result of one compile assurance command. </summary>
internal sealed record CompileExecutionResult
{
    private const string SuccessMessage = "Unity compile assurance completed.";

    private CompileExecutionResult (
        CompileExecutionOutput? output,
        ProjectIdentityInfo? project,
        IReadOnlyList<ApplicationFailure> errors)
    {
        Output = output;
        Project = project;
        Errors = errors;
    }

    /// <summary> Gets the output payload when execution reached verifier completion. </summary>
    public CompileExecutionOutput? Output { get; }

    /// <summary> Gets the resolved project when available. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Gets command failure errors. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets a value indicating whether command execution reached verifier completion. </summary>
    public bool IsSuccess => Output != null && Errors.Count == 0;

    /// <summary> Gets the user-facing command message. </summary>
    public string Message => IsSuccess ? SuccessMessage : Errors.FirstOrDefault()?.Message ?? "Unity compile assurance failed.";

    /// <summary> Creates a completed compile execution result. </summary>
    public static CompileExecutionResult Success (CompileExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new CompileExecutionResult(output, output.Project, []);
    }

    /// <summary> Creates a failed compile command result. </summary>
    public static CompileExecutionResult Failure (
        ExecutionError error,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failure(ApplicationFailure.FromExecutionError(error), project);
    }

    /// <summary> Creates a failed compile command result. </summary>
    public static CompileExecutionResult Failure (
        ApplicationFailure failure,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new CompileExecutionResult(null, project, [failure]);
    }
}
