using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;

/// <summary> Represents one completed or failed compile assurance command execution. </summary>
internal abstract record CompileExecutionResult
{
    private const string SuccessMessage = "Unity compile assurance completed.";

    private CompileExecutionResult ()
    {
    }

    /// <summary> Creates a completed compile execution result. </summary>
    public static CompletedResult Completed (CompileExecutionOutput output)
    {
        return new CompletedResult(output);
    }

    /// <summary> Creates a failed compile command result. </summary>
    public static FailedResult Failed (
        ExecutionError error,
        ProjectIdentityInfo? project)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failed(ApplicationFailure.FromExecutionError(error), project);
    }

    /// <summary> Creates a failed compile command result. </summary>
    public static FailedResult Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project)
    {
        return new FailedResult(failure, project);
    }

    /// <summary> Represents completed verifier execution with a compile assurance packet. </summary>
    internal sealed record CompletedResult : CompileExecutionResult
    {
        internal CompletedResult (CompileExecutionOutput output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary> Gets the completed compile assurance output. </summary>
        public CompileExecutionOutput Output { get; }

        /// <summary> Gets the user-facing completion message. </summary>
        public string Message => SuccessMessage;
    }

    /// <summary> Represents command failure before a compile assurance packet was produced. </summary>
    internal sealed record FailedResult : CompileExecutionResult
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
