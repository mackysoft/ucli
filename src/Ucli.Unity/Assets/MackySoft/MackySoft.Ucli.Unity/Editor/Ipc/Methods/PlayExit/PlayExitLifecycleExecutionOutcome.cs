using System;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Represents the typed delivery outcome produced by the Play Mode exit
    /// lifecycle execution owner before the IPC adapter encodes it.
    /// </summary>
    internal sealed class PlayExitLifecycleExecutionOutcome
    {
        private PlayExitLifecycleExecutionOutcome (
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            PlayLifecycleTransitionResult result,
            PlayExitLifecycleExecutionError error,
            bool hasActionPayload)
        {
            LifecycleExecutionRef = lifecycleExecutionRef;
            ApplicationState = applicationState;
            Result = result;
            Error = error;
            HasActionPayload = hasActionPayload;
        }

        public ExecutionRef LifecycleExecutionRef { get; }

        public ExecutionApplicationState ApplicationState { get; }

        public PlayLifecycleTransitionResult Result { get; }

        public PlayExitLifecycleExecutionError Error { get; }

        public bool HasActionPayload { get; }

        public bool IsSuccess => Error == null;

        public static PlayExitLifecycleExecutionOutcome Completed (
            TerminalExecutionRef terminalReference,
            PlayLifecycleTransitionResult result)
        {
            return new PlayExitLifecycleExecutionOutcome(
                terminalReference,
                result.OutcomeApplicationState,
                result,
                error: null,
                hasActionPayload: true);
        }

        public static PlayExitLifecycleExecutionOutcome Failed (
            UcliCode code,
            string message,
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            PlayLifecycleTransitionResult result,
            string instancePath = null,
            bool hasActionPayload = true)
        {
            return new PlayExitLifecycleExecutionOutcome(
                lifecycleExecutionRef,
                applicationState,
                result,
                new PlayExitLifecycleExecutionError(
                    code,
                    message,
                    instancePath),
                hasActionPayload);
        }
    }

    /// <summary>
    /// Retains one Play Mode exit failure without coupling the action state to
    /// an IPC response DTO.
    /// </summary>
    internal sealed record PlayExitLifecycleExecutionError
    {
        [JsonConstructor]
        public PlayExitLifecycleExecutionError (
            UcliCode code,
            string message,
            string instancePath)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = string.IsNullOrWhiteSpace(message)
                ? throw new ArgumentException(
                    "Play Mode exit execution error message must not be blank.",
                    nameof(message))
                : message;
            InstancePath = instancePath;
        }

        public UcliCode Code { get; }

        public string Message { get; }

        public string InstancePath { get; }
    }
}
