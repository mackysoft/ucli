using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one completed or failed <c>ready</c> command execution. </summary>
internal abstract record ReadyExecutionResult
{
    private ReadyExecutionResult ()
    {
    }

    /// <summary> Creates a completed ready result. </summary>
    public static CompletedResult Completed (ReadyExecutionOutput output)
    {
        return new CompletedResult(output);
    }

    /// <summary> Creates a failed command result from a structured execution error. </summary>
    public static FailedResult Failed (
        ExecutionError error,
        ProjectIdentityInfo? project)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failed(ApplicationFailure.FromExecutionError(error), project);
    }

    /// <summary> Creates a failed command result from an application failure. </summary>
    public static FailedResult Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project)
    {
        return new FailedResult(failure, project);
    }

    /// <summary> Represents completed verifier execution with a ready assurance packet. </summary>
    internal sealed record CompletedResult : ReadyExecutionResult
    {
        internal CompletedResult (ReadyExecutionOutput output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary> Gets the completed ready assurance output. </summary>
        public ReadyExecutionOutput Output { get; }

        /// <summary> Gets the user-facing completion message. </summary>
        public string Message => Output.Verdict == Verdict.Pass
            ? "uCLI ready assurance passed."
            : "uCLI ready assurance completed.";
    }

    /// <summary> Represents command failure before a ready assurance packet was produced. </summary>
    internal sealed record FailedResult : ReadyExecutionResult
    {
        internal FailedResult (
            ApplicationFailure failure,
            ProjectIdentityInfo? project)
        {
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
            Project = project;
        }

        /// <summary> Gets the failure that prevented verifier completion. </summary>
        public ApplicationFailure Failure { get; }

        /// <summary> Gets the resolved project when project resolution completed. </summary>
        public ProjectIdentityInfo? Project { get; }

        /// <summary> Gets the user-facing failure message. </summary>
        public string Message => Failure.Message;
    }
}
