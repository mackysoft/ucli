using System;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Represents the typed delivery outcome produced by the compile lifecycle
    /// execution owner before the IPC adapter encodes it.
    /// </summary>
    internal sealed class CompileLifecycleExecutionOutcome
    {
        private CompileLifecycleExecutionOutcome (
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            CompileLifecycleResult result,
            UnityEditorObservation observedLifecycle,
            CompileLifecycleExecutionError error,
            bool hasActionPayload)
        {
            LifecycleExecutionRef = lifecycleExecutionRef;
            ApplicationState = applicationState;
            Result = result;
            ObservedLifecycle = observedLifecycle;
            Error = error;
            HasActionPayload = hasActionPayload;
        }

        public ExecutionRef LifecycleExecutionRef { get; }

        public ExecutionApplicationState ApplicationState { get; }

        public CompileLifecycleResult Result { get; }

        public UnityEditorObservation ObservedLifecycle { get; }

        public CompileLifecycleExecutionError Error { get; }

        public bool HasActionPayload { get; }

        public bool IsSuccess => Error == null;

        public static CompileLifecycleExecutionOutcome Completed (
            TerminalExecutionRef terminalReference,
            CompileLifecycleResult result)
        {
            return new CompileLifecycleExecutionOutcome(
                terminalReference,
                ExecutionApplicationState.Applied,
                result,
                observedLifecycle: null,
                error: null,
                hasActionPayload: true);
        }

        public static CompileLifecycleExecutionOutcome Failed (
            UcliCode code,
            string message,
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            CompileLifecycleResult result,
            UnityEditorObservation observedLifecycle,
            string instancePath = null,
            bool hasActionPayload = true)
        {
            return new CompileLifecycleExecutionOutcome(
                lifecycleExecutionRef,
                applicationState,
                result,
                observedLifecycle,
                new CompileLifecycleExecutionError(
                    code,
                    message,
                    instancePath),
                hasActionPayload);
        }
    }

    /// <summary>
    /// Retains one compile failure without coupling the action state to an IPC
    /// response DTO.
    /// </summary>
    internal sealed record CompileLifecycleExecutionError
    {
        [JsonConstructor]
        public CompileLifecycleExecutionError (
            UcliCode code,
            string message,
            string instancePath)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = string.IsNullOrWhiteSpace(message)
                ? throw new ArgumentException(
                    "Compile execution error message must not be blank.",
                    nameof(message))
                : message;
            InstancePath = instancePath;
        }

        public UcliCode Code { get; }

        public string Message { get; }

        public string InstancePath { get; }
    }
}
