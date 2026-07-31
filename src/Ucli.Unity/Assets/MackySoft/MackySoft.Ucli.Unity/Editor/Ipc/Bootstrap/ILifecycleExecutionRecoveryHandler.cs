using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Recovers one action-owned Lifecycle Execution after common host admission has been evaluated.
    /// </summary>
    internal interface ILifecycleExecutionRecoveryHandler
    {
        LifecycleExecutionKind Kind { get; }

        ValueTask RecoverAsync (
            LifecycleExecutionRecoveryRequest request,
            CancellationToken cancellationToken);
    }

    /// <summary> Carries the durable binding and any typed common recovery rejection. </summary>
    internal sealed record LifecycleExecutionRecoveryRequest
    {
        public LifecycleExecutionRecoveryRequest (
            LifecycleExecutionStartBinding start,
            LifecycleExecutionTerminalReason? rejectionReason,
            bool canAttributeCurrentProviderObservation)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            RejectionReason = rejectionReason;
            CanAttributeCurrentProviderObservation =
                canAttributeCurrentProviderObservation;
        }

        public LifecycleExecutionStartBinding Start { get; }

        public LifecycleExecutionTerminalReason? RejectionReason { get; }

        /// <summary>
        /// Gets whether common recovery admission proved that observations from the current
        /// provider endpoint belong to this durable execution.
        /// </summary>
        public bool CanAttributeCurrentProviderObservation { get; }
    }
}
