using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Recording;

/// <summary>Preserves the latest durable execution checkpoint returned with a recording command failure.</summary>
internal sealed record GameViewRecordingErrorCommandPayload
    : CommandErrorPayload<GameViewRecordingErrorCommandPayload>
{
    public GameViewRecordingErrorCommandPayload (GameViewRecordingExecutionPayload execution)
    {
        Execution = execution ?? throw new ArgumentNullException(nameof(execution));
    }

    public GameViewRecordingExecutionPayload Execution { get; }
}
