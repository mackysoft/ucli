using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Captures the process and endpoint registration that may host Lifecycle Execution requests.
    /// </summary>
    internal sealed class UnityLifecycleExecutionHostContext
    {
        public UnityLifecycleExecutionHostContext (
            ProcessIdentity process,
            Guid editorInstanceId,
            Guid endpointRegistrationGenerationId,
            DaemonLifecycleRecoveryLease recoveryLease)
        {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            if (editorInstanceId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Editor instance identifier must not be empty.",
                    nameof(editorInstanceId));
            }

            if (endpointRegistrationGenerationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Endpoint registration generation must not be empty.",
                    nameof(endpointRegistrationGenerationId));
            }

            EditorInstanceId = editorInstanceId;
            EndpointRegistrationGenerationId = endpointRegistrationGenerationId;
            RecoveryLease = recoveryLease;
        }

        public ProcessIdentity Process { get; }

        public Guid EditorInstanceId { get; }

        public Guid EndpointRegistrationGenerationId { get; }

        public DaemonLifecycleRecoveryLease RecoveryLease { get; }

        public LifecycleExecutionHostRegistration CreateInitialRegistration ()
        {
            return new LifecycleExecutionHostRegistration(
                Process,
                EditorInstanceId,
                EndpointRegistrationGenerationId,
                EndpointRegistrationGenerationId);
        }
    }
}
