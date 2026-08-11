using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Recording;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary>Defines the output contracts for GameView recording commands.</summary>
internal static class UcliRecordingCommandOutputContracts
{
    internal static UcliCommandOutputContract RecordingStart { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.RecordingStart,
            GameViewRecordingCommandResultFactory.ExecutionSuccessPayloadTypeInfo,
            GameViewRecordingCommandResultFactory.ErrorPayloadTypeInfo,
            GameViewRecordingCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract RecordingStatus { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.RecordingStatus,
            GameViewRecordingCommandResultFactory.StatusSuccessPayloadTypeInfo,
            GameViewRecordingCommandResultFactory.ErrorPayloadTypeInfo,
            GameViewRecordingCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract RecordingStop { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.RecordingStop,
            GameViewRecordingCommandResultFactory.StopSuccessPayloadTypeInfo,
            GameViewRecordingCommandResultFactory.ErrorPayloadTypeInfo,
            GameViewRecordingCommandResultFactory.CreateEmptyErrorPayload);
}
