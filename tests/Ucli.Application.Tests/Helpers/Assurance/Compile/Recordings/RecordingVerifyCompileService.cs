using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingVerifyCompileService : ICompileService
{
    private readonly Func<CompileCommandInput, CompileExecutionResult> resultFactory;
    private readonly List<Invocation> invocations = [];

    public RecordingVerifyCompileService (Func<CompileCommandInput, CompileExecutionResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<CompileExecutionResult> ExecuteAsync (
        CompileCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(input, progressSink, cancellationToken));
        return ValueTask.FromResult(resultFactory(input));
    }

    internal readonly record struct Invocation (
        CompileCommandInput Input,
        ICommandProgressSink? ProgressSink,
        CancellationToken CancellationToken);
}
