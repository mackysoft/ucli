using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;

/// <summary> Represents one completed or failed verify command execution. </summary>
internal abstract record VerifyExecutionResult
{
    private VerifyExecutionResult ()
    {
    }

    /// <summary> Creates a completed verify execution result. </summary>
    public static CompletedResult Completed (VerifyExecutionOutput output)
    {
        return new CompletedResult(output);
    }

    /// <summary> Creates a failed verify execution result from an application failure. </summary>
    public static FailedResult Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project)
    {
        return new FailedResult(failure, project);
    }

    /// <summary> Creates a failed verify execution result from an execution error. </summary>
    public static FailedResult Failed (
        ExecutionError error,
        ProjectIdentityInfo? project)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failed(ApplicationFailure.FromExecutionError(error), project);
    }

    /// <summary> Represents completed verifier execution with a verify assurance packet. </summary>
    internal sealed record CompletedResult : VerifyExecutionResult
    {
        internal CompletedResult (VerifyExecutionOutput output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary> Gets the completed verify assurance output. </summary>
        public VerifyExecutionOutput Output { get; }

        /// <summary> Gets the user-facing completion message. </summary>
        public string Message => "Verify assurance completed.";
    }

    /// <summary> Represents command failure before a verify assurance packet was produced. </summary>
    internal sealed record FailedResult : VerifyExecutionResult
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
