using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Features.Init.UseCases.Init;

namespace MackySoft.Tests;

internal sealed class RecordingInitService : RecordingCommandService<InitCommandInput, InitExecutionResult>, IInitService
{
    public RecordingInitService (Func<InitCommandInput, CancellationToken, ValueTask<InitExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<InitExecutionResult> ExecuteAsync (
        InitCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
