using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Requests;

/// <summary> Normalizes one execute request into a strict request contract used by execution pipelines. </summary>
internal interface IExecuteRequestNormalizer
{
    /// <summary> Validates and normalizes one execute request payload. </summary>
    /// <param name="request"> The execute request payload. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
    /// <returns> The normalization result that contains either normalized request data or one structured error. </returns>
    ExecuteRequestNormalizationResult Normalize (
        IpcExecuteRequest request,
        CancellationToken cancellationToken = default);
}
