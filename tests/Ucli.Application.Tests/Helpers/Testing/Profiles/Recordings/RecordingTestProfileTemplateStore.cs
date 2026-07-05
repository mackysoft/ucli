using MackySoft.Ucli.Application.Features.Testing.Profiles;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Ports;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingTestProfileTemplateStore : ITestProfileTemplateStore
{
    private readonly TestProfileInitExecutionResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingTestProfileTemplateStore (TestProfileInitExecutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<TestProfileInitExecutionResult> WriteAsync (
        TestProfile profile,
        string? outputPath,
        bool force,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            profile,
            outputPath,
            force,
            cancellationToken));

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        TestProfile Profile,
        string? OutputPath,
        bool Force,
        CancellationToken CancellationToken);
}
