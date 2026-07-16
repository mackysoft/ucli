using System;

#nullable enable

using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation failure entry captured by phase execution. </summary>
    /// <param name="Code"> The machine-readable error code. </param>
    /// <param name="Message"> The human-readable error message. </param>
    /// <param name="OpId"> The related operation identifier, or <see langword="null" /> when unavailable. </param>
    public sealed record OperationFailure
    {
        public OperationFailure (
            UcliCode Code,
            string Message,
            IpcExecuteStepId? OpId)
        {
            this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
            this.Message = Message;
            this.OpId = OpId;
        }

        public UcliCode Code { get; }

        public string Message { get; }

        public IpcExecuteStepId? OpId { get; }
    }
}
