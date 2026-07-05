using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

namespace MackySoft.Tests;

internal sealed class RecordingCallService : RecordingCommandService<CallCommandInput, CallServiceResult>, ICallService
{
    public RecordingCallService (
        Func<CallCommandInput, CancellationToken, ValueTask<CallServiceResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<CallServiceResult> ExecuteAsync (
        CallCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
