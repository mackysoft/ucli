using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Features.Init.Ports;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingInitTemplateStore : IInitTemplateStore
{
    private readonly InitExecutionResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingInitTemplateStore (InitExecutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<InitExecutionResult> WriteAsync (
        UcliConfig config,
        bool force,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            config,
            force,
            cancellationToken));

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        UcliConfig Config,
        bool Force,
        CancellationToken CancellationToken);
}
