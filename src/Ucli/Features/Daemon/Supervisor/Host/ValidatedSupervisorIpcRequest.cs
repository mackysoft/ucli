using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Carries one authorized supervisor IPC request after protocol and literal validation. </summary>
internal sealed class ValidatedSupervisorIpcRequest : IIpcRequestCorrelation
{
    /// <summary> Initializes one validated supervisor IPC request. </summary>
    public ValidatedSupervisorIpcRequest (
        Guid requestId,
        SupervisorIpcMethod method,
        JsonElement payload,
        IpcResponseMode responseMode,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        if (!TextVocabulary.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Supervisor IPC method must be defined.");
        }

        if (!TextVocabulary.IsDefined(responseMode))
        {
            throw new ArgumentOutOfRangeException(nameof(responseMode), responseMode, "IPC response mode must be defined.");
        }

        if (requestDeadlineRemainingMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestDeadlineRemainingMilliseconds),
                requestDeadlineRemainingMilliseconds,
                "Request deadline remaining milliseconds must be greater than zero.");
        }

        RequestId = requestId;
        Method = method;
        Payload = payload;
        ResponseMode = responseMode;
        RequestDeadlineUtc = ContractArgumentGuard.RequireUtcTimestamp(
            requestDeadlineUtc,
            nameof(requestDeadlineUtc));
        RequestDeadlineRemainingMilliseconds = requestDeadlineRemainingMilliseconds;
    }

    /// <inheritdoc />
    public Guid RequestId { get; }

    /// <summary> Gets the defined supervisor IPC method. </summary>
    public SupervisorIpcMethod Method { get; }

    /// <summary> Gets the method-specific request payload. </summary>
    public JsonElement Payload { get; }

    /// <summary> Gets the defined response framing mode. </summary>
    public IpcResponseMode ResponseMode { get; }

    /// <summary> Gets the UTC deadline shared by every delivery attempt for the logical request. </summary>
    public DateTimeOffset RequestDeadlineUtc { get; }

    /// <summary> Gets the positive monotonic-clock time remaining when this delivery attempt started. </summary>
    public int RequestDeadlineRemainingMilliseconds { get; }
}
