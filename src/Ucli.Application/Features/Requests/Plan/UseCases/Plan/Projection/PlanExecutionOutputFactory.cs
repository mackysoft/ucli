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
        Guid requestId,
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);

        return new PlanExecutionOutput(
            requestId: requestId,
            project: ProjectIdentityInfo.From(preparedRequest.ProjectContext.UnityProject),
            opResults: [],
            readIndex: readIndex,
            planToken: null);
    }

    /// <summary> Tries to create the base plan payload for failure projection. </summary>
    /// <param name="preparedRequest"> The prepared request context when available. </param>
    /// <param name="readIndex"> The emitted read-index payload when available. </param>
    /// <returns> The base payload, or <see langword="null" /> when the minimum data are unavailable. </returns>
    public static PlanExecutionOutput? TryCreateBase (
        Guid requestId,
        PreparedRequestContext? preparedRequest,
        ReadIndexInfo? readIndex)
    {
        if (preparedRequest == null
            || readIndex == null)
        {
            return null;
        }

        return CreateBase(requestId, preparedRequest, readIndex);
    }
}
