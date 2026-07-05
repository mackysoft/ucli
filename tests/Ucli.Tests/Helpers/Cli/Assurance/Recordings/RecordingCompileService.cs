using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Tests;

internal sealed class RecordingCompileService : RecordingProgressCommandService<CompileCommandInput, CompileExecutionResult>, ICompileService
{
    public RecordingCompileService (
        Func<CompileCommandInput, ICommandProgressSink?, CancellationToken, ValueTask<CompileExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<CompileExecutionResult> ExecuteAsync (
        CompileCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, progressSink, cancellationToken);
    }
}
