using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Represents one normalized public step and the number of compiled primitives it produced. </summary>
    /// <param name="Id"> The public step identifier. </param>
    /// <param name="Kind"> The public step kind. </param>
    /// <param name="OperationName"> The public operation name reported by execute responses. </param>
    /// <param name="PrimitiveCount"> The number of compiled primitives emitted for this step. </param>
    internal sealed record NormalizedRequestStep (
        string Id,
        IpcRequestStepKind Kind,
        string OperationName,
        int PrimitiveCount)
    {
        /// <summary> Gets diagnostics emitted while compiling this public step. </summary>
        public IReadOnlyList<IpcExecuteDiagnostic> Diagnostics { get; init; } = System.Array.Empty<IpcExecuteDiagnostic>();
    }
}
