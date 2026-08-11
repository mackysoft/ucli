using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonCommandExecutionContextResolver : IDaemonCommandExecutionContextResolver
{
    private readonly DaemonCommandExecutionContextResolutionResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonCommandExecutionContextResolver (DaemonCommandExecutionContextResolutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonCommandExecutionContextResolutionResult> ResolveAsync (
        UcliCommand timeoutCommand,
        AbsolutePath? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(
            timeoutCommand,
            projectPath,
            timeoutMilliseconds,
            cancellationToken));

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        UcliCommand TimeoutCommand,
        AbsolutePath? ProjectPath,
        int? TimeoutMilliseconds,
        CancellationToken CancellationToken);
}
