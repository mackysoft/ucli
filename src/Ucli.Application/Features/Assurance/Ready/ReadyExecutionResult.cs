using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents the application result returned by the <c>ready</c> use case. </summary>
internal sealed record ReadyExecutionResult
{
    private ReadyExecutionResult (
        ReadyExecutionOutput? output,
        IReadOnlyList<ApplicationFailure> errors,
        string message,
        ProjectIdentityInfo? project)
    {
        Output = output;
        Errors = errors;
        Message = message;
        Project = project;
    }

    /// <summary> Gets the assurance output on success; otherwise <see langword="null" />. </summary>
    public ReadyExecutionOutput? Output { get; }

    /// <summary> Gets command-level failures that prevented an assurance packet from being produced. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the user-facing result message. </summary>
    public string Message { get; }

    /// <summary> Gets the resolved project identity when project resolution succeeded. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Gets a value indicating whether the command produced an assurance packet. </summary>
    public bool IsSuccess => Output is not null && Errors.Count == 0;

    /// <summary> Creates a successful ready result. </summary>
    public static ReadyExecutionResult Success (ReadyExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new ReadyExecutionResult(
            output,
            Array.Empty<ApplicationFailure>(),
            output.Verdict == AssuranceVerdict.Pass
                ? "uCLI ready assurance passed."
                : "uCLI ready assurance completed.",
            output.Project);
    }

    /// <summary> Creates a failed command-level result from a structured execution error. </summary>
    public static ReadyExecutionResult Failure (
        ExecutionError error,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        var failure = ApplicationFailure.FromExecutionError(error);
        return new ReadyExecutionResult(
            output: null,
            [failure],
            failure.Message,
            project);
    }

    /// <summary> Creates a failed command-level result from an application failure. </summary>
    public static ReadyExecutionResult Failure (
        ApplicationFailure failure,
        ProjectIdentityInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new ReadyExecutionResult(
            output: null,
            [failure],
            failure.Message,
            project);
    }
}
