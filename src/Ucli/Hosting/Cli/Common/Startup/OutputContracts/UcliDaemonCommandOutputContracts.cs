using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Hosting.Cli.Screenshot;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines output contracts for daemon, log, and screenshot commands. </summary>
internal static class UcliDaemonCommandOutputContracts
{
    internal static UcliCommandOutputContract DaemonStart { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.DaemonStart,
            DaemonStartCommand.SuccessPayloadTypeInfo,
            DaemonStartCommand.ErrorPayloadTypeInfo,
            DaemonStartCommand.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract DaemonStop { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.DaemonStop,
            DaemonStopCommand.SuccessPayloadTypeInfo,
            DaemonStopCommand.ErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract DaemonCleanup { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.DaemonCleanup,
            DaemonCleanupCommand.SuccessPayloadTypeInfo,
            DaemonCleanupCommand.ErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract DaemonStatus { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.DaemonStatus,
            UcliCommandOutputContracts.ResolveTypeInfo<DaemonStatusExecutionOutput>(),
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract DaemonList { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.DaemonList,
            DaemonListCommand.SuccessPayloadTypeInfo,
            DaemonListCommand.ErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract LogsDaemonRead { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.LogsDaemonRead,
            LogsDaemonReadCommand.SuccessPayloadTypeInfo,
            LogsDaemonReadCommand.ErrorPayloadTypeInfo,
            LogsReadCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract LogsUnityRead { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.LogsUnityRead,
            LogsUnityReadCommand.SuccessPayloadTypeInfo,
            LogsUnityReadCommand.ErrorPayloadTypeInfo,
            LogsReadCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract LogsUnityClear { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.LogsUnityClear,
            LogsUnityClearCommand.SuccessPayloadTypeInfo,
            LogsUnityClearCommand.ErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract ScreenshotGame { get; } =
        CreateScreenshot(UcliCommandNames.ScreenshotGame);

    internal static UcliCommandOutputContract ScreenshotScene { get; } =
        CreateScreenshot(UcliCommandNames.ScreenshotScene);

    private static UcliCommandOutputContract CreateScreenshot (string command)
    {
        return UcliCommandOutputContracts.Complete(
            command,
            ScreenshotCommandResultFactory.SuccessPayloadTypeInfo,
            ScreenshotCommandResultFactory.ErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);
    }
}
