using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;

/// <summary> Builds plan command payloads from prepared request state. </summary>
internal static class PlanExecutionOutputFactory
{
    /// <summary> Creates the base plan payload when request and read-index data are available. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <returns> The base payload. </returns>
    public static PlanExecutionOutput CreateBase (
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(preparedRequest.Request.RequestId);

        return new PlanExecutionOutput(
            RequestId: preparedRequest.Request.RequestId,
            Project: ProjectIdentityInfo.From(preparedRequest.ProjectContext.UnityProject),
            OpResults: [],
            ContractViolations: [],
            ReadIndex: readIndex,
            PlanToken: null);
    }

    /// <summary> Tries to create the base plan payload for failure projection. </summary>
    /// <param name="preparedRequest"> The prepared request context when available. </param>
    /// <param name="readIndex"> The emitted read-index payload when available. </param>
    /// <returns> The base payload, or <see langword="null" /> when the minimum data are unavailable. </returns>
    public static PlanExecutionOutput? TryCreateBase (
        PreparedRequestContext? preparedRequest,
        ReadIndexInfo? readIndex)
    {
        if (preparedRequest == null
            || readIndex == null
            || string.IsNullOrWhiteSpace(preparedRequest.Request.RequestId))
        {
            return null;
        }

        return CreateBase(preparedRequest, readIndex);
    }
}
