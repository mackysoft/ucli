using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Skills;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines output contracts for Agent Skills and test commands. </summary>
internal static class UcliSkillsCommandOutputContracts
{
    internal static UcliCommandOutputContract SkillsList { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.SkillsList,
            SkillsListCommandPayloadFactory.TypeInfo,
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract SkillsExport { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.SkillsExport,
            SkillsCommandResultFactory.ExportSuccessPayloadTypeInfo,
            SkillsCommandResultFactory.ExportErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract SkillsInstall { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.SkillsInstall,
            SkillsCommandResultFactory.InstallSuccessPayloadTypeInfo,
            SkillsCommandResultFactory.InstallErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract SkillsUpdate { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.SkillsUpdate,
            SkillsCommandResultFactory.UpdateSuccessPayloadTypeInfo,
            SkillsCommandResultFactory.UpdateErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract SkillsUninstall { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.SkillsUninstall,
            SkillsCommandResultFactory.UninstallSuccessPayloadTypeInfo,
            SkillsCommandResultFactory.UninstallErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract SkillsPrune { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.SkillsPrune,
            SkillsCommandResultFactory.PruneSuccessPayloadTypeInfo,
            SkillsCommandResultFactory.PruneErrorPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract SkillsDoctor { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.SkillsDoctor,
            SkillsCommandResultFactory.DoctorSuccessPayloadTypeInfo,
            SkillsCommandResultFactory.DoctorErrorPayloadTypeInfo,
            CommandErrorPayload.Empty<SkillsDoctorCommandPayload>);

    internal static UcliCommandOutputContract TestRun { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.TestRun,
            TestRunCommandResultFactory.SuccessPayloadTypeInfo,
            TestRunCommandResultFactory.ErrorPayloadTypeInfo,
            TestRunCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract TestProfileInit { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.TestProfileInit,
            UcliCommandOutputContracts.ResolveTypeInfo<TestProfileInitExecutionOutput>(),
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);
}
