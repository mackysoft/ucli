using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

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
        ProjectIdentityInfo? project,
        ExecutionRef? lifecycleExecutionRef,
        ExecutionApplicationState applicationState)
    {
        return Failed(
            error,
            project,
            lifecycleExecutionRef,
            applicationState,
            result: null,
            observedLifecycle: null);
    }

    /// <summary> Creates a failed compile command result with confirmed action observations. </summary>
    public static FailedResult Failed (
        ExecutionError error,
        ProjectIdentityInfo? project,
        ExecutionRef? lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        CompileLifecycleResult? result,
        UnityEditorObservation? observedLifecycle)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failed(
            ApplicationFailure.FromExecutionError(error),
            project,
            lifecycleExecutionRef,
            applicationState,
            result,
            observedLifecycle);
    }

    /// <summary> Creates a failed compile command result. </summary>
    public static FailedResult Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project,
        ExecutionRef? lifecycleExecutionRef,
        ExecutionApplicationState applicationState)
    {
        return Failed(
            failure,
            project,
            lifecycleExecutionRef,
            applicationState,
            result: null,
            observedLifecycle: null);
    }

    /// <summary> Creates a failed compile command result with confirmed action observations. </summary>
    public static FailedResult Failed (
        ApplicationFailure failure,
        ProjectIdentityInfo? project,
        ExecutionRef? lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        CompileLifecycleResult? result,
        UnityEditorObservation? observedLifecycle)
    {
        return new FailedResult(
            failure,
            project,
            lifecycleExecutionRef,
            applicationState,
            result,
            observedLifecycle);
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
            ProjectIdentityInfo? project,
            ExecutionRef? lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            CompileLifecycleResult? result,
            UnityEditorObservation? observedLifecycle)
        {
            if (!TextVocabulary.IsDefined(applicationState))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(applicationState),
                    applicationState,
                    "Compile application state must be defined.");
            }
            if (lifecycleExecutionRef == null
                && applicationState != ExecutionApplicationState.NotApplied)
            {
                throw new ArgumentException(
                    "A compile failure without a registered Lifecycle Execution must be notApplied.",
                    nameof(applicationState));
            }
            if (lifecycleExecutionRef == null && result != null)
            {
                throw new ArgumentException(
                    "A compile result cannot exist before Lifecycle Execution registration.",
                    nameof(result));
            }
            if (lifecycleExecutionRef != null)
            {
                var compileDefinition = new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.Compile);
                if (lifecycleExecutionRef.Kind != compileDefinition.ExecutionKind
                    || lifecycleExecutionRef.DefinitionDigest
                        != LifecycleExecutionDefinitionDigest.Calculate(compileDefinition))
                {
                    throw new ArgumentException(
                        "Compile failure requires a compile Lifecycle Execution reference.",
                        nameof(lifecycleExecutionRef));
                }
            }

            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
            Project = project;
            LifecycleExecutionRef = lifecycleExecutionRef;
            ApplicationState = applicationState;
            Result = result;
            ObservedLifecycle = observedLifecycle;
        }

        /// <summary> Gets the failure that prevented verifier completion. </summary>
        public ApplicationFailure Failure { get; }

        /// <summary> Gets the resolved project when project resolution completed. </summary>
        public ProjectIdentityInfo? Project { get; }

        /// <summary> Gets the registered execution reference when the durable start was confirmed. </summary>
        public ExecutionRef? LifecycleExecutionRef { get; }

        /// <summary> Gets the confirmed application state at the failure boundary. </summary>
        public ExecutionApplicationState ApplicationState { get; }

        /// <summary> Gets the typed compile result when one was confirmed before delivery failed. </summary>
        public CompileLifecycleResult? Result { get; }

        /// <summary> Gets the last complete Unity lifecycle observed by the compile action. </summary>
        public UnityEditorObservation? ObservedLifecycle { get; }

        /// <summary> Gets the user-facing failure message. </summary>
        public string Message => Failure.Message;
    }
}
