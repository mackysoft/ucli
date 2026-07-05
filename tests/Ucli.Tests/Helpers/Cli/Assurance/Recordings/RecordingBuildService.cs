using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal sealed class RecordingBuildService : RecordingProgressCommandService<BuildCommandInput, BuildExecutionResult>, IBuildService
{
    public RecordingBuildService (
        Func<BuildCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<BuildExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<BuildExecutionResult> ExecuteAsync (
        BuildCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, progressSink, cancellationToken);
    }
}
