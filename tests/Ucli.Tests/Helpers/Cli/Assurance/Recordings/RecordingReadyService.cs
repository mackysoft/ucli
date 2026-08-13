using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Tests;

internal sealed class RecordingReadyService : RecordingCommandService<ReadyCommandInput, ReadyExecutionResult>, IReadyService
{
    public RecordingReadyService (Func<ReadyCommandInput, CancellationToken, ValueTask<ReadyExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<ReadyExecutionResult> ExecuteAsync (
        ReadyCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }

    public ValueTask<ProgramReadyObservation> ObserveOnFixedHostAsync (
        ProjectContext context,
        IUnityExecutionHostBinding binding,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default) => throw new InvalidOperationException();
}
