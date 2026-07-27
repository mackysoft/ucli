using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines output contracts for assurance commands. </summary>
internal static class UcliAssuranceCommandOutputContracts
{
    internal static UcliCommandOutputContract Ready { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Ready,
            ReadyCommandResultFactory.SuccessPayloadTypeInfo,
            ReadyCommandResultFactory.ErrorPayloadTypeInfo,
            ReadyCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract Compile { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Compile,
            CompileCommandResultFactory.SuccessPayloadTypeInfo,
            CompileCommandResultFactory.ErrorPayloadTypeInfo,
            CompileCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract Verify { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Verify,
            VerifyCommandResultFactory.SuccessPayloadTypeInfo,
            VerifyCommandResultFactory.ErrorPayloadTypeInfo,
            VerifyCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract BuildRun { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.BuildRun,
            BuildRunCommandResultFactory.SuccessPayloadTypeInfo,
            BuildRunCommandResultFactory.ErrorPayloadTypeInfo,
            BuildRunCommandResultFactory.CreateEmptyErrorPayload);
}
