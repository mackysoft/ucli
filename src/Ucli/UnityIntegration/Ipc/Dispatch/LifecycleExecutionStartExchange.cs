using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary>
/// Creates and interprets the provider-private Start exchange for one typed Lifecycle Execution
/// dispatch.
/// </summary>
internal static class LifecycleExecutionStartExchange
{
    /// <summary> Represents one completely interpreted Start exchange outcome. </summary>
    internal abstract record Interpretation;

    /// <summary> Carries the provider-confirmed Start binding and action payload derived from it. </summary>
    internal sealed record Confirmed (
        LifecycleExecutionStartBinding Start,
        JsonElement ActionPayload) : Interpretation;

    /// <summary> Carries a typed provider rejection that remains an IPC response. </summary>
    internal sealed record ProviderRejected (IpcResponse Response) : Interpretation;

    /// <summary> Carries a conflict with the authoritative Start binding used for reconnect. </summary>
    internal sealed record Mismatched (UcliCode Code) : Interpretation;

    /// <summary> Carries an invalid provider response or binding as an application-level failure. </summary>
    internal sealed record Invalid (UnityRequestFailure Failure) : Interpretation;

    /// <summary>
    /// Creates the provider-private Start request after the owning client has fixed the delivery
    /// deadline and transport session.
    /// </summary>
    public static IpcRequestEnvelope CreateRequest (
        UnityIpcDispatchRequest dispatchRequest,
        IpcSessionToken sessionToken,
        Guid requestId,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(sessionToken);
        return UnityIpcRequestFactory.Create(
            sessionToken,
            UnityIpcMethod.LifecycleStart,
            IpcPayloadCodec.SerializeToElement(
                dispatchRequest.CreateLifecycleStartRequest()),
            requestId,
            IpcResponseMode.Single,
            requestDeadlineUtc,
            requestDeadlineRemainingMilliseconds);
    }

    /// <summary>
    /// Decodes one successful provider response, validates it against the authoritative reconnect
    /// binding, and derives the typed action payload. Provider rejections remain responses so the
    /// action-owned admission policy can interpret their typed errors.
    /// </summary>
    public static Interpretation InterpretResponse (
        UnityIpcDispatchRequest dispatchRequest,
        IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(response);
        if (response.Status == IpcResponseStatus.Error)
        {
            return new ProviderRejected(response);
        }

        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcLifecycleExecutionStartResponse lifecycleStart,
                out var lifecycleStartReadError))
        {
            return new Invalid(
                UnityIpcFailureClassifier.InternalError(
                    "Lifecycle Execution start response is invalid. "
                    + lifecycleStartReadError.Message));
        }

        try
        {
            var confirmedStart = lifecycleStart.Start;
            var mismatchCode =
                dispatchRequest.GetRequiredStartMismatchCode(
                    confirmedStart);
            if (mismatchCode is not null)
            {
                return new Mismatched(mismatchCode);
            }

            return new Confirmed(
                confirmedStart,
                dispatchRequest.CreateLifecycleActionPayload(
                    confirmedStart));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return new Invalid(
                UnityIpcFailureClassifier.InternalError(
                    "Lifecycle Execution start binding is invalid. "
                    + exception.Message));
        }
    }
}
