using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

namespace MackySoft.Tests;

internal sealed class RecordingCallService : RecordingCommandService<CallCommandInput, CallServiceResult>, ICallService
{
    private readonly List<Guid> requestIds = [];

    public RecordingCallService (
        Func<CallCommandInput, CancellationToken, ValueTask<CallServiceResult>> handler)
        : base(handler)
    {
    }

    public IReadOnlyList<Guid> RequestIds => requestIds;

    public ValueTask<CallServiceResult> ExecuteAsync (
        Guid requestId,
        CallCommandInput input,
        CancellationToken cancellationToken = default)
    {
        requestIds.Add(requestId);
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
