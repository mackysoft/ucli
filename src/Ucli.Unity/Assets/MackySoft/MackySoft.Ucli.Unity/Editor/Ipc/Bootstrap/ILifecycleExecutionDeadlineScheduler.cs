using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Tracks one durable Lifecycle Execution until its immutable deadline independently of caller wait lifetime.
    /// </summary>
    internal interface ILifecycleExecutionDeadlineScheduler
    {
        /// <summary> Ensures the open execution is recovered with its typed deadline reason when due. </summary>
        /// <param name="kind"> The action-owned Lifecycle Execution kind. </param>
        /// <param name="executionId"> The durable execution identifier. </param>
        void Track (
            LifecycleExecutionKind kind,
            Guid executionId);
    }
}
