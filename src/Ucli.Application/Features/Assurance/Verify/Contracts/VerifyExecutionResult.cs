using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;

/// <summary> Represents the result of one verify command execution. </summary>
internal sealed record VerifyExecutionResult
{
    private VerifyExecutionResult (
        VerifyExecutionOutput? output,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ProjectIdentityInfo? project)
    {
        Output = output;
        Errors = errors;
        Message = message;
        Project = project;
    }

    /// <summary> Gets a value indicating whether the command produced a verify payload. </summary>
    public bool IsSuccess => Output != null;

    /// <summary> Gets the verify output when execution succeeded. </summary>
    public VerifyExecutionOutput? Output { get; }

    /// <summary> Gets command failures when execution failed before producing a verify payload. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the command message. </summary>
    public string Message { get; }

    /// <summary> Gets the resolved project identity when available. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Creates a successful verify execution result. </summary>
    public static VerifyExecutionResult Success (VerifyExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new VerifyExecutionResult(output, [], "Verify assurance completed.", output.Project);
    }

    /// <summary> Creates a failed verify execution result from an application failure. </summary>
    public static VerifyExecutionResult Failure (
        ApplicationFailure failure,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new VerifyExecutionResult(null, [failure], failure.Message, project);
    }

    /// <summary> Creates a failed verify execution result from an execution error. </summary>
    public static VerifyExecutionResult Failure (
        ExecutionError error,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failure(ApplicationFailure.FromExecutionError(error), project);
    }
}
