using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;

/// <summary> Builds call command payloads from prepared request state. </summary>
internal static class CallExecutionOutputFactory
{
    /// <summary> Creates the base call payload when the prepared request is available. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <returns> The base payload. </returns>
    public static CallExecutionOutput CreateBase (PreparedRequestContext preparedRequest)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(preparedRequest.Request.RequestId);

        return new CallExecutionOutput(
            RequestId: preparedRequest.Request.RequestId,
            Project: ProjectIdentityInfo.From(preparedRequest.ProjectContext.UnityProject),
            OpResults: [],
            Plan: null,
            ReadPostcondition: null,
            PostReadSource: null);
    }

    /// <summary> Tries to create the base call payload for failure projection. </summary>
    /// <param name="preparedRequest"> The prepared request context when available. </param>
    /// <returns> The base payload, or <see langword="null" /> when the request identifier is unavailable. </returns>
    public static CallExecutionOutput? TryCreateBase (PreparedRequestContext? preparedRequest)
    {
        if (preparedRequest == null
            || string.IsNullOrWhiteSpace(preparedRequest.Request.RequestId))
        {
            return null;
        }

        return CreateBase(preparedRequest);
    }
}
