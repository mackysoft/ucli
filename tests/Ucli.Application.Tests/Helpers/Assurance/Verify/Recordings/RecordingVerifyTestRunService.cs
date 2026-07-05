using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingVerifyTestRunService : ITestRunService
{
    private readonly Func<TestRunCommandInput, TestRunServiceResult> resultFactory;
    private readonly List<Invocation> invocations = [];

    public RecordingVerifyTestRunService (Func<TestRunCommandInput, TestRunServiceResult> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<TestRunServiceResult> ExecuteAsync (
        TestRunCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(input, progressSink, cancellationToken));
        return ValueTask.FromResult(resultFactory(input));
    }

    internal readonly record struct Invocation (
        TestRunCommandInput Input,
        ICommandProgressSink? ProgressSink,
        CancellationToken CancellationToken);
}
