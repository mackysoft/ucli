using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;

/// <summary> Represents one completed or failed build assurance command execution. </summary>
internal abstract record BuildExecutionResult
{
    private const string SuccessMessage = "Build completed.";

    private BuildExecutionResult ()
    {
    }

    /// <summary> Creates a completed build execution result. </summary>
    public static CompletedResult Completed (BuildExecutionOutput output)
    {
        return new CompletedResult(output);
    }

    /// <summary> Creates a failed build command result without dirty-state evidence. </summary>
    public static FailedResult Failed (
        ExecutionError error,
        ProjectIdentityInfo? project)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failed(ApplicationFailure.FromExecutionError(error), project);
    }

    /// <summary> Creates a failed build command result without dirty-state evidence. </summary>
    public static FailedResult Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project)
    {
        return new FailedResult(failure, project);
    }

    /// <summary> Creates a failed build command result with observed dirty-state evidence. </summary>
    public static DirtyStateFailedResult FailedWithDirtyState (
        ExecutionError error,
        ProjectIdentityInfo project,
        IpcBuildDirtyState dirtyState)
    {
        ArgumentNullException.ThrowIfNull(error);
        return FailedWithDirtyState(
            ApplicationFailure.FromExecutionError(error),
            project,
            dirtyState);
    }

    /// <summary> Creates a failed build command result with observed dirty-state evidence. </summary>
    public static DirtyStateFailedResult FailedWithDirtyState (
        ApplicationFailure failure,
        ProjectIdentityInfo project,
        IpcBuildDirtyState dirtyState)
    {
        return new DirtyStateFailedResult(failure, project, dirtyState);
    }

    /// <summary> Represents completed verifier execution with a build assurance packet. </summary>
    internal sealed record CompletedResult : BuildExecutionResult
    {
        internal CompletedResult (BuildExecutionOutput output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary> Gets the completed build assurance output. </summary>
        public BuildExecutionOutput Output { get; }

        /// <summary> Gets the user-facing completion message. </summary>
        public string Message => SuccessMessage;
    }

    /// <summary> Represents command failure without observed dirty-state evidence. </summary>
    internal sealed record FailedResult : BuildExecutionResult
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

    /// <summary> Represents command failure with observed dirty-state evidence. </summary>
    internal sealed record DirtyStateFailedResult : BuildExecutionResult
    {
        internal DirtyStateFailedResult (
            ApplicationFailure failure,
            ProjectIdentityInfo project,
            IpcBuildDirtyState dirtyState)
        {
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
            Project = project ?? throw new ArgumentNullException(nameof(project));
            DirtyState = dirtyState ?? throw new ArgumentNullException(nameof(dirtyState));
        }

        /// <summary> Gets the failure that prevented verifier completion. </summary>
        public ApplicationFailure Failure { get; }

        /// <summary> Gets the resolved project. </summary>
        public ProjectIdentityInfo Project { get; }

        /// <summary> Gets the dirty-state evidence observed for the failed precondition. </summary>
        public IpcBuildDirtyState DirtyState { get; }

        /// <summary> Gets the user-facing failure message. </summary>
        public string Message => Failure.Message;
    }
}
