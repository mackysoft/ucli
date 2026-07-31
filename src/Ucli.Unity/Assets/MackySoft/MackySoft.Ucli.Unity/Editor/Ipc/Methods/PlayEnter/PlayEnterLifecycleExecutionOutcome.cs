using System;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Represents the typed delivery outcome produced by the Play Mode enter
    /// lifecycle execution owner before the IPC adapter encodes it.
    /// </summary>
    internal sealed class PlayEnterLifecycleExecutionOutcome
    {
        private PlayEnterLifecycleExecutionOutcome (
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            PlayLifecycleTransitionResult result,
            PlayEnterLifecycleExecutionError error,
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

        public PlayEnterLifecycleExecutionError Error { get; }

        public bool HasActionPayload { get; }

        public bool IsSuccess => Error == null;

        public static PlayEnterLifecycleExecutionOutcome Completed (
            TerminalExecutionRef terminalReference,
            PlayLifecycleTransitionResult result)
        {
            return new PlayEnterLifecycleExecutionOutcome(
                terminalReference,
                result.OutcomeApplicationState,
                result,
                error: null,
                hasActionPayload: true);
        }

        public static PlayEnterLifecycleExecutionOutcome Failed (
            UcliCode code,
            string message,
            ExecutionRef lifecycleExecutionRef,
            ExecutionApplicationState applicationState,
            PlayLifecycleTransitionResult result,
            string instancePath = null,
            bool hasActionPayload = true)
        {
            return new PlayEnterLifecycleExecutionOutcome(
                lifecycleExecutionRef,
                applicationState,
                result,
                new PlayEnterLifecycleExecutionError(
                    code,
                    message,
                    instancePath),
                hasActionPayload);
        }
    }

    /// <summary>
    /// Retains one Play Mode entry failure without coupling the action state to
    /// an IPC response DTO.
    /// </summary>
    internal sealed record PlayEnterLifecycleExecutionError
    {
        [JsonConstructor]
        public PlayEnterLifecycleExecutionError (
            UcliCode code,
            string message,
            string instancePath)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = string.IsNullOrWhiteSpace(message)
                ? throw new ArgumentException(
                    "Play Mode entry execution error message must not be blank.",
                    nameof(message))
                : message;
            InstancePath = instancePath;
        }

        public UcliCode Code { get; }

        public string Message { get; }

        public string InstancePath { get; }
    }
}
