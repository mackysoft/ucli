using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal sealed class RecordingVerifyService : RecordingProgressCommandService<VerifyCommandInput, VerifyExecutionResult>, IVerifyService
{
    public RecordingVerifyService (
        Func<VerifyCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<VerifyExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<VerifyExecutionResult> ExecuteAsync (
        VerifyCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, progressSink, cancellationToken);
    }
}
