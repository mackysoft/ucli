using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal sealed class RecordingTestRunService : RecordingProgressCommandService<TestRunCommandInput, TestRunServiceResult>, ITestRunService
{
    public RecordingTestRunService (
        Func<TestRunCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<TestRunServiceResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<TestRunServiceResult> ExecuteAsync (
        TestRunCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, progressSink, cancellationToken);
    }
}
