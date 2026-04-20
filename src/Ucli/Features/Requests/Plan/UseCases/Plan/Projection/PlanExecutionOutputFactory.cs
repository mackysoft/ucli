using MackySoft.Ucli.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Projection;

/// <summary> Builds plan command payloads from prepared request state. </summary>
internal static class PlanExecutionOutputFactory
{
    /// <summary> Creates the base plan payload when request and read-index data are available. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <returns> The base payload, or <see langword="null" /> when the minimum data are unavailable. </returns>
    public static PlanExecutionOutput? CreateBase (
        PreparedRequestContext? preparedRequest,
        ReadIndexInfo? readIndex)
    {
        if (preparedRequest == null
            || readIndex == null
            || string.IsNullOrWhiteSpace(preparedRequest.Request.RequestId))
        {
            return null;
        }

        return new PlanExecutionOutput(
            RequestId: preparedRequest.Request.RequestId,
            OpResults: [],
            ReadIndex: readIndex,
            PlanToken: null);
    }
}