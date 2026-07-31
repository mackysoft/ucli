using System;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Represents the typed delivery outcome produced by the refresh lifecycle
    /// execution owner before the IPC adapter encodes it.
    /// </summary>
    internal sealed class RefreshLifecycleExecutionOutcome
    {
        private RefreshLifecycleExecutionOutcome (
            UnityProjectIdentity project,
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            RefreshLifecycleResult result,
            RefreshLifecycleStartEvidence refresh,
            UnityEditorObservation observedLifecycle,
            ExecutionReadPostcondition readPostcondition,
            RefreshLifecycleExecutionError error,
            bool hasActionPayload)
        {
            Project = project;
            LifecycleExecutionRef = lifecycleExecutionRef;
            ApplicationState = applicationState;
            Result = result;
            Refresh = refresh;
            ObservedLifecycle = observedLifecycle;
            ReadPostcondition = readPostcondition;
            Error = error;
            HasActionPayload = hasActionPayload;
        }

        public UnityProjectIdentity Project { get; }

        public ExecutionRef LifecycleExecutionRef { get; }

        public ExecutionApplicationState ApplicationState { get; }

        public RefreshLifecycleResult Result { get; }

        public RefreshLifecycleStartEvidence Refresh { get; }

        public UnityEditorObservation ObservedLifecycle { get; }

        public ExecutionReadPostcondition ReadPostcondition { get; }

        public RefreshLifecycleExecutionError Error { get; }

        public bool HasActionPayload { get; }

        public bool IsSuccess => Error == null;

        public static RefreshLifecycleExecutionOutcome Completed (
            UnityProjectIdentity project,
            TerminalExecutionRef terminalReference,
            RefreshLifecycleResult result)
        {
            return new RefreshLifecycleExecutionOutcome(
                project,
                terminalReference,
                ExecutionApplicationState.Applied,
                result,
                refresh: null,
                observedLifecycle: null,
                readPostcondition: null,
                error: null,
                hasActionPayload: true);
        }

        public static RefreshLifecycleExecutionOutcome Failed (
            UnityProjectIdentity project,
            UcliCode code,
            string message,
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            RefreshLifecycleResult result,
            RefreshLifecycleStartEvidence refresh,
            UnityEditorObservation observedLifecycle,
            ExecutionReadPostcondition readPostcondition,
            string instancePath = null,
            bool hasActionPayload = true)
        {
            return new RefreshLifecycleExecutionOutcome(
                project,
                lifecycleExecutionRef,
                applicationState,
                result,
                refresh,
                observedLifecycle,
                readPostcondition,
                new RefreshLifecycleExecutionError(
                    code,
                    message,
                    instancePath),
                hasActionPayload);
        }
    }

    /// <summary>
    /// Retains one refresh failure without coupling the action state to an IPC
    /// response DTO.
    /// </summary>
    internal sealed record RefreshLifecycleExecutionError
    {
        [JsonConstructor]
        public RefreshLifecycleExecutionError (
            UcliCode code,
            string message,
            string instancePath)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = string.IsNullOrWhiteSpace(message)
                ? throw new ArgumentException(
                    "Refresh execution error message must not be blank.",
                    nameof(message))
                : message;
            InstancePath = instancePath;
        }

        public UcliCode Code { get; }

        public string Message { get; }

        public string InstancePath { get; }
    }
}
