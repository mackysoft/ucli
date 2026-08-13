using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup;

/// <summary> Defines the public command tree consumed by <see cref="UcliCommandCatalog" />. </summary>
internal static class UcliCommandCatalogDefinition
{
    internal static readonly UcliCommandCatalog.StandaloneCommandEntry[] StandaloneCommands =
    [
        new(UcliCoreCommandOutputContracts.Init),
        new(UcliCoreCommandOutputContracts.Status),
        new(UcliAssuranceCommandOutputContracts.Ready),
        new(UcliAssuranceCommandOutputContracts.Compile),
        new(UcliAssuranceCommandOutputContracts.Verify),
        new(UcliRequestCommandOutputContracts.Refresh),
        new(UcliRequestCommandOutputContracts.Resolve),
        new(UcliRequestCommandOutputContracts.Validate),
        new(UcliRequestCommandOutputContracts.Plan),
        new(UcliRequestCommandOutputContracts.Call),
        new(UcliRequestCommandOutputContracts.Eval),
    ];

    internal static readonly UcliCommandCatalog.CommandGroupEntry[] CommandGroups = UcliCommandGroupDefinitions.All;

    internal static readonly UcliCommandCatalog.UnexpectedLeafArgumentRule[] UnexpectedLeafArgumentRules =
    [
        new(
            UcliCommandNames.Skills,
            UcliCommandNames.ListSubcommand,
            UcliCommandNames.SkillsList,
            ExpectedArgumentCount: 2),
    ];
}

/// <summary> Defines the root and nested command groups in the public command tree. </summary>
internal static class UcliCommandGroupDefinitions
{
    internal static readonly UcliCommandCatalog.CommandGroupEntry[] All =
    [
        new(
            UcliCommandNames.Daemon,
            [
                Leaf(UcliCommandNames.StartSubcommand, UcliDaemonCommandOutputContracts.DaemonStart),
                Leaf(UcliCommandNames.StopSubcommand, UcliDaemonCommandOutputContracts.DaemonStop),
                Leaf(UcliCommandNames.CleanupSubcommand, UcliDaemonCommandOutputContracts.DaemonCleanup),
                Leaf(UcliCommandNames.Status, UcliDaemonCommandOutputContracts.DaemonStatus),
                Leaf(UcliCommandNames.ListSubcommand, UcliDaemonCommandOutputContracts.DaemonList),
            ],
            []),
        new(
            UcliCommandNames.Logs,
            [],
            [
                Nested(
                    UcliCommandNames.Daemon,
                    [
                        Leaf(UcliCommandNames.ReadSubcommand, UcliDaemonCommandOutputContracts.LogsDaemonRead),
                    ]),
                Nested(
                    UcliCommandNames.UnitySubcommand,
                    [
                        Leaf(UcliCommandNames.ReadSubcommand, UcliDaemonCommandOutputContracts.LogsUnityRead),
                        Leaf(UcliCommandNames.ClearSubcommand, UcliDaemonCommandOutputContracts.LogsUnityClear),
                    ]),
            ]),
        new(
            UcliCommandNames.Screenshot,
            [
                Leaf(UcliCommandNames.GameSubcommand, UcliDaemonCommandOutputContracts.ScreenshotGame),
                Leaf(UcliCommandNames.SceneSubcommand, UcliDaemonCommandOutputContracts.ScreenshotScene),
            ],
            []),
        new(
            UcliCommandNames.Recording,
            [
                Leaf(UcliCommandNames.StartSubcommand, UcliRecordingCommandOutputContracts.RecordingStart),
                Leaf(UcliCommandNames.Status, UcliRecordingCommandOutputContracts.RecordingStatus),
                Leaf(UcliCommandNames.StopSubcommand, UcliRecordingCommandOutputContracts.RecordingStop),
            ],
            []),
        new(
            UcliCommandNames.Ops,
            [
                Leaf(UcliCommandNames.ListSubcommand, UcliCatalogCommandOutputContracts.OpsList),
                Leaf(UcliCommandNames.DescribeSubcommand, UcliCatalogCommandOutputContracts.OpsDescribe),
            ],
            []),
        new(
            UcliCommandNames.Schema,
            [
                Leaf(UcliCommandNames.ListSubcommand, UcliCatalogCommandOutputContracts.SchemaList),
                Leaf(UcliCommandNames.GetSubcommand, UcliCatalogCommandOutputContracts.SchemaGet),
                Leaf(UcliCommandNames.ExportSubcommand, UcliCatalogCommandOutputContracts.SchemaExport),
            ],
            []),
        new(
            UcliCommandNames.Codes,
            [
                Leaf(UcliCommandNames.ListSubcommand, UcliCatalogCommandOutputContracts.CodesList),
                Leaf(UcliCommandNames.DescribeSubcommand, UcliCatalogCommandOutputContracts.CodesDescribe),
            ],
            []),
        new(
            UcliCommandNames.Play,
            [
                Leaf(UcliCommandNames.Status, UcliCatalogCommandOutputContracts.PlayStatus),
                Leaf(UcliCommandNames.EnterSubcommand, UcliCatalogCommandOutputContracts.PlayEnter),
                Leaf(UcliCommandNames.ExitSubcommand, UcliCatalogCommandOutputContracts.PlayExit),
            ],
            []),
        new(
            UcliCommandNames.Skills,
            [
                Leaf(UcliCommandNames.ListSubcommand, UcliSkillsCommandOutputContracts.SkillsList),
                Leaf(UcliCommandNames.ExportSubcommand, UcliSkillsCommandOutputContracts.SkillsExport),
                Leaf(UcliCommandNames.InstallSubcommand, UcliSkillsCommandOutputContracts.SkillsInstall),
                Leaf(UcliCommandNames.UpdateSubcommand, UcliSkillsCommandOutputContracts.SkillsUpdate),
                Leaf(UcliCommandNames.UninstallSubcommand, UcliSkillsCommandOutputContracts.SkillsUninstall),
                Leaf(UcliCommandNames.PruneSubcommand, UcliSkillsCommandOutputContracts.SkillsPrune),
                Leaf(UcliCommandNames.DoctorSubcommand, UcliSkillsCommandOutputContracts.SkillsDoctor),
            ],
            []),
        new(
            UcliCommandNames.Query,
            [],
            [
                Nested(
                    UcliCommandNames.AssetsSubcommand,
                    [
                        Leaf(UcliCommandNames.FindSubcommand, UcliRequestCommandOutputContracts.QueryAssetsFind),
                    ]),
                Nested(
                    UcliCommandNames.SceneSubcommand,
                    [
                        Leaf(UcliCommandNames.TreeSubcommand, UcliRequestCommandOutputContracts.QuerySceneTree),
                    ]),
                Nested(
                    UcliCommandNames.GoSubcommand,
                    [
                        Leaf(UcliCommandNames.DescribeSubcommand, UcliRequestCommandOutputContracts.QueryGoDescribe),
                    ]),
                Nested(
                    UcliCommandNames.CompSubcommand,
                    [
                        Leaf(UcliCommandNames.SchemaSubcommand, UcliRequestCommandOutputContracts.QueryCompSchema),
                    ]),
                Nested(
                    UcliCommandNames.AssetSubcommand,
                    [
                        Leaf(UcliCommandNames.SchemaSubcommand, UcliRequestCommandOutputContracts.QueryAssetSchema),
                    ]),
            ]),
        new(
            UcliCommandNames.Test,
            [
                Leaf(UcliCommandNames.RunSubcommand, UcliSkillsCommandOutputContracts.TestRun),
            ],
            [
                Nested(
                    UcliCommandNames.Profile,
                    [
                        Leaf(UcliCommandNames.InitSubcommand, UcliSkillsCommandOutputContracts.TestProfileInit),
                    ]),
            ]),
        new(
            UcliCommandNames.Build,
            [
                Leaf(UcliCommandNames.RunSubcommand, UcliAssuranceCommandOutputContracts.BuildRun),
            ],
            []),
        new(
            UcliCommandNames.Program,
            [
                Leaf(UcliCommandNames.Validate, UcliProgramCommandOutputContracts.Validate),
                Leaf(UcliCommandNames.Plan, UcliProgramCommandOutputContracts.Plan),
                Leaf(UcliCommandNames.RunSubcommand, UcliProgramCommandOutputContracts.Run),
                Leaf(UcliCommandNames.Status, UcliProgramCommandOutputContracts.Status),
                Leaf(UcliCommandNames.CancelSubcommand, UcliProgramCommandOutputContracts.Cancel),
            ],
            [
                Nested(
                    UcliCommandNames.Presets,
                    [
                        Leaf(UcliCommandNames.ListSubcommand, UcliProgramCommandOutputContracts.PresetsList),
                        Leaf(UcliCommandNames.DescribeSubcommand, UcliProgramCommandOutputContracts.PresetsDescribe),
                    ]),
            ]),
    ];

    private static UcliCommandCatalog.CommandLeafEntry Leaf (
        string subcommandName,
        UcliCommandOutputContract outputContract)
    {
        return new UcliCommandCatalog.CommandLeafEntry(
            subcommandName,
            outputContract);
    }

    private static UcliCommandCatalog.NestedCommandGroupEntry Nested (
        string groupName,
        UcliCommandCatalog.CommandLeafEntry[] leaves)
    {
        return new UcliCommandCatalog.NestedCommandGroupEntry(
            groupName,
            leaves);
    }
}
