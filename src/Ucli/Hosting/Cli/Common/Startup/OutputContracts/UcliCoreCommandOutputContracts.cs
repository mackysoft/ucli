using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Features.Status.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines root, command-group, and standalone core output contracts. </summary>
internal static class UcliCoreCommandOutputContracts
{
    internal static UcliCommandOutputContract Init { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Init,
            UcliCommandOutputContracts.ResolveTypeInfo<InitExecutionOutput>(),
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract Status { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Status,
            UcliCommandOutputContracts.ResolveTypeInfo<StatusExecutionOutput>(),
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);
}
