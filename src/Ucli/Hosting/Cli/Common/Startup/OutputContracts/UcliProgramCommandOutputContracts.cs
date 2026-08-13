using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Programs;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines output contracts for the public Program command surface. </summary>
internal static class UcliProgramCommandOutputContracts
{
    internal static UcliCommandOutputContract PresetsList { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.ProgramPresetsList,
            ProgramCommandResultFactory.PresetListPayloadTypeInfo,
            ProgramCommandResultFactory.PresetListPayloadTypeInfo,
            static () => new ProgramPresetListPayload(null, [], []));

    internal static UcliCommandOutputContract PresetsDescribe { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.ProgramPresetsDescribe,
            ProgramCommandResultFactory.PresetDescribePayloadTypeInfo,
            ProgramCommandResultFactory.PresetDescribePayloadTypeInfo,
            static () => new ProgramPresetDescribePayload(null, null, null, null, null, null, null, null, []));
    internal static UcliCommandOutputContract Validate { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.ProgramValidate,
            ProgramCommandResultFactory.ValidationPayloadTypeInfo,
            ProgramCommandResultFactory.ValidationPayloadTypeInfo,
            static () => new ProgramValidationPayload(null, false, null, null, []));

    internal static UcliCommandOutputContract Plan { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.ProgramPlan,
            ProgramCommandResultFactory.PlanPayloadTypeInfo,
            ProgramCommandResultFactory.PlanPayloadTypeInfo,
            ProgramPlanPayload.Empty);
    internal static UcliCommandOutputContract Run { get; } = CreateRun(UcliCommandNames.ProgramRun);
    internal static UcliCommandOutputContract Status { get; } = CreateRun(UcliCommandNames.ProgramStatus);
    internal static UcliCommandOutputContract Cancel { get; } = CreateRun(UcliCommandNames.ProgramCancel);

    private static UcliCommandOutputContract CreateRun (string command) =>
        UcliCommandOutputContracts.Complete(
            command,
            ProgramCommandResultFactory.RunPayloadTypeInfo,
            ProgramCommandResultFactory.RunPayloadTypeInfo,
            ProgramRunStatusPayload.NotFound);
}
