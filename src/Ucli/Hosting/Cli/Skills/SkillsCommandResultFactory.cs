using System.Text.Json.Serialization.Metadata;
using MackySoft.AgentSkills.Doctor;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosting.Commands;
using MackySoft.AgentSkills.Hosts.Contracts;
using MackySoft.AgentSkills.Installation.Results;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.OperationReports.Contracts;
using MackySoft.AgentSkills.OperationReports.Literals;
using MackySoft.AgentSkills.Shared;
using MackySoft.AgentSkills.Shared.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Creates command-level JSON results for <c>skills</c> commands. </summary>
internal static class SkillsCommandResultFactory
{
    private const string PrivateVarPath = "/private/var";
    private const string VarPath = "/var";

    /// <summary> Gets the serializer contract used by successful <c>skills export</c> payloads. </summary>
    public static JsonTypeInfo ExportSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(SkillsExportCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>skills export</c> payloads. </summary>
    public static JsonTypeInfo ExportErrorPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(EmptyCommandPayload));

    /// <summary> Gets the serializer contract used by successful <c>skills install</c> payloads. </summary>
    public static JsonTypeInfo InstallSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(SkillsInstallCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>skills install</c> payloads. </summary>
    public static JsonTypeInfo InstallErrorPayloadTypeInfo { get; } = ExportErrorPayloadTypeInfo;

    /// <summary> Gets the serializer contract used by successful <c>skills update</c> payloads. </summary>
    public static JsonTypeInfo UpdateSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(SkillsUpdateCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>skills update</c> payloads. </summary>
    public static JsonTypeInfo UpdateErrorPayloadTypeInfo { get; } = ExportErrorPayloadTypeInfo;

    /// <summary> Gets the serializer contract used by successful <c>skills uninstall</c> payloads. </summary>
    public static JsonTypeInfo UninstallSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(SkillsUninstallCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>skills uninstall</c> payloads. </summary>
    public static JsonTypeInfo UninstallErrorPayloadTypeInfo { get; } = ExportErrorPayloadTypeInfo;

    /// <summary> Gets the serializer contract used by successful <c>skills prune</c> payloads. </summary>
    public static JsonTypeInfo PruneSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(SkillsPruneCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>skills prune</c> payloads. </summary>
    public static JsonTypeInfo PruneErrorPayloadTypeInfo { get; } = ExportErrorPayloadTypeInfo;

    /// <summary> Gets the serializer contract used by successful <c>skills doctor</c> payloads. </summary>
    public static JsonTypeInfo DoctorSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(SkillsDoctorCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>skills doctor</c> payloads. </summary>
    public static JsonTypeInfo DoctorErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<SkillsDoctorCommandPayload>();

    /// <summary> Creates a command result from the shared Agent Skills command runtime result. </summary>
    public static CommandResult Create (AgentSkillsCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            return CreateSkillFailure(result.Command, result.Failure!);
        }

        return result.Payload switch
        {
            SkillListReport report => CommandResult.Success(
                result.Command,
                "uCLI official SKILL package list retrieval completed.",
                SkillsListCommandPayloadFactory.Create(report)),
            SkillExportReport report => CommandResult.Success(
                result.Command,
                "uCLI official SKILL packages exported.",
                CreateExportPayload(report)),
            SkillOperationReport report => CommandResult.Success(
                result.Command,
                CreateOperationMessage(result.Command, report),
                CreateOperationPayload(result.Command, report)),
            SkillDoctorReport report => CreateDoctor(result.Command, report),
            _ => CommandFailureProjector.Create(
                result.Command,
                ApplicationFailure.InternalError(
                    $"Unsupported Agent Skills command payload: {result.Payload?.GetType().FullName ?? "(null)"}"),
                CreateSkillFailurePayload(result.Command)),
        };
    }

    /// <summary> Creates one command failure from a SKILL library failure. </summary>
    /// <param name="command"> The command name. </param>
    /// <param name="failure"> The SKILL operation failure. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateSkillFailure (
        string command,
        SkillFailure failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(failure);

        return CommandFailureProjector.Create(
            command,
            SkillFailureApplicationFailureMapper.Map(failure),
            CreateSkillFailurePayload(command));
    }

    private static SkillsExportCommandPayload CreateExportPayload (SkillExportReport report)
    {
        return new SkillsExportCommandPayload(
            UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(report.Host),
            report.Categories,
            report.SkillNames,
            UcliSkillCommandVocabularyMapper.Map<SkillExportFormat, UcliSkillExportFormat>(report.Format),
            ToDisplayPath(report.OutputPath),
            report.Skills,
            report.SkillCount,
            report.ReloadGuidance);
    }

    private static object CreateOperationPayload (
        string command,
        SkillOperationReport report)
    {
        var targetRoot = ToDisplayPath(report.TargetRoot);
        var repositoryRoot = report.RepositoryRoot is null ? null : ToDisplayPath(report.RepositoryRoot);

        return command switch
        {
            UcliCommandNames.SkillsInstall => new SkillsInstallCommandPayload(
                UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(report.Host),
                report.Categories,
                report.SkillNames,
                UcliSkillCommandVocabularyMapper.Map<SkillScopeKind, UcliSkillScope>(report.Scope),
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                HasDiffs(report),
                report.ReloadGuidance,
                CreateActionPayloads<UcliSkillInstallAction>(report.Actions, targetRoot),
                CountAction(report, SkillInstallActionKind.Created),
                CountAction(report, SkillInstallActionKind.Updated),
                CountAction(report, SkillInstallActionKind.NoOp),
                CountBlocked(report)),
            UcliCommandNames.SkillsUpdate => new SkillsUpdateCommandPayload(
                UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(report.Host),
                report.Categories,
                report.SkillNames,
                UcliSkillCommandVocabularyMapper.Map<SkillScopeKind, UcliSkillScope>(report.Scope),
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                HasDiffs(report),
                report.ReloadGuidance,
                CreateActionPayloads<UcliSkillUpdateAction>(report.Actions, targetRoot),
                CountAction(report, SkillUpdateActionKind.Created),
                CountAction(report, SkillUpdateActionKind.Updated),
                CountAction(report, SkillUpdateActionKind.NoOp),
                CountBlocked(report)),
            UcliCommandNames.SkillsUninstall => new SkillsUninstallCommandPayload(
                UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(report.Host),
                report.Categories,
                report.SkillNames,
                UcliSkillCommandVocabularyMapper.Map<SkillScopeKind, UcliSkillScope>(report.Scope),
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                report.ReloadGuidance,
                CreateActionPayloads<UcliSkillUninstallAction>(report.Actions, targetRoot),
                CountAction(report, SkillUninstallActionKind.Deleted),
                CountAction(report, SkillUninstallActionKind.NoOp),
                CountAction(report, SkillUninstallActionKind.SkippedUnmanaged),
                CountBlocked(report)),
            UcliCommandNames.SkillsPrune => new SkillsPruneCommandPayload(
                UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(report.Host),
                report.Categories,
                report.SkillNames,
                UcliSkillCommandVocabularyMapper.Map<SkillScopeKind, UcliSkillScope>(report.Scope),
                repositoryRoot,
                targetRoot,
                report.DryRun,
                report.Force,
                report.ReloadGuidance,
                CreateActionPayloads<UcliSkillPruneAction>(report.Actions, targetRoot),
                CountAction(report, SkillPruneActionKind.Deleted),
                CountAction(report, SkillPruneActionKind.SkippedCurrent),
                CountAction(report, SkillPruneActionKind.SkippedForeignCatalog),
                CountAction(report, SkillPruneActionKind.SkippedUnmanaged),
                CountBlocked(report)),
            _ => throw new InvalidOperationException($"Unsupported Agent Skills operation command: {command}."),
        };
    }

    private static CommandResult CreateDoctor (
        string command,
        SkillDoctorReport report)
    {
        var targetRoot = ToDisplayPath(report.TargetRoot);
        var payload = new SkillsDoctorCommandPayload(
            UcliSkillCommandVocabularyMapper.Map<SkillHostKind, UcliOfficialSkillHost>(report.Host),
            report.Categories,
            report.SkillNames,
            UcliSkillCommandVocabularyMapper.Map<SkillScopeKind, UcliSkillScope>(report.Scope),
            report.RepositoryRoot is null ? null : ToDisplayPath(report.RepositoryRoot),
            targetRoot,
            report.ReloadGuidance,
            report.IsHealthy,
            report.Diagnostics
                .Select(static diagnostic => new SkillsDoctorDiagnosticCommandPayload(
                    UcliSkillCommandVocabularyMapper.Map<SkillDoctorSeverity, UcliSkillDoctorSeverity>(
                        diagnostic.Severity),
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.SkillName))
                .ToArray());

        if (report.IsHealthy)
        {
            return CommandResult.Success(
                command,
                "uCLI official SKILL packages are healthy.",
                payload);
        }

        var failures = report.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == SkillDoctorSeverity.Error)
            .Select(static diagnostic => ApplicationFailure.InternalError(diagnostic.Message, new UcliCode(diagnostic.Code)))
            .ToArray();
        if (failures.Length == 0)
        {
            failures =
            [
                ApplicationFailure.InternalError("uCLI skills doctor reported an unknown error."),
            ];
        }

        return CommandFailureProjector.Create(
            command,
            "uCLI skills doctor reported errors.",
            CommandErrorPayload.Detailed(payload),
            failures);
    }

    private static string CreateOperationMessage (
        string command,
        SkillOperationReport report)
    {
        return command switch
        {
            UcliCommandNames.SkillsInstall => report.DryRun ? "uCLI official SKILL install plan generated." : "uCLI official SKILL packages installed.",
            UcliCommandNames.SkillsUpdate => report.DryRun ? "uCLI official SKILL update plan generated." : "uCLI official SKILL packages updated.",
            UcliCommandNames.SkillsUninstall => report.DryRun ? "uCLI official SKILL uninstall plan generated." : "uCLI official SKILL packages uninstalled.",
            UcliCommandNames.SkillsPrune => report.DryRun ? "uCLI official SKILL prune plan generated." : "uCLI official SKILL packages pruned.",
            _ => throw new InvalidOperationException($"Unsupported Agent Skills operation command: {command}."),
        };
    }

    private static IReadOnlyList<SkillsOperationActionCommandPayload<TAction>> CreateActionPayloads<TAction> (
        IReadOnlyList<SkillOperationActionReport> actions,
        string targetRoot)
        where TAction : struct, Enum
    {
        return actions
            .Select(action => new SkillsOperationActionCommandPayload<TAction>(
                action.SkillName,
                UcliSkillCommandVocabularyMapper.Parse<TAction>(
                    action.Action,
                    nameof(SkillOperationActionReport)),
                targetRoot,
                action.BlockedReason is null
                    ? null
                    : UcliSkillCommandVocabularyMapper.Map<SkillBlockedReason, UcliSkillBlockedReason>(
                        action.BlockedReason.Value),
                CreateDiffPayloads(action.FileDiffs)))
            .ToArray();
    }

    private static IReadOnlyList<SkillsOperationDiffCommandPayload> CreateDiffPayloads (
        IReadOnlyList<SkillOperationFileDiffReport> fileDiffs)
    {
        if (fileDiffs.Count == 0)
        {
            return [];
        }

        return
        [
            new SkillsOperationDiffCommandPayload(
                fileDiffs
                    .Select(static file => new SkillsOperationFileDiffCommandPayload(
                        file.RelativePath,
                        UcliSkillCommandVocabularyMapper.Map<SkillDiffChangeKind, UcliSkillDiffChangeKind>(
                            file.ChangeKind),
                        file.BeforeContent,
                        file.AfterContent))
                    .ToArray()),
        ];
    }

    private static int CountAction<TAction> (
        SkillOperationReport report,
        TAction action)
        where TAction : struct, Enum
    {
        return report.Actions.Count(candidate => ContractLiteralCodec.Matches(candidate.Action, action));
    }

    private static int CountBlocked (SkillOperationReport report)
    {
        return report.Actions.Count(static action =>
            action.Status == SkillOperationActionStatus.Blocked
            || action.BlockedReason is not null);
    }

    private static bool HasDiffs (SkillOperationReport report)
    {
        return report.Actions.Any(static action => action.FileDiffs.Count > 0);
    }

    private static object CreateSkillFailurePayload (string command)
    {
        return string.Equals(command, UcliCommandNames.SkillsDoctor, StringComparison.Ordinal)
            ? CommandErrorPayload.Empty<SkillsDoctorCommandPayload>()
            : EmptyCommandPayload.Instance;
    }

    private static string ToDisplayPath (string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return path;
        }

        if (string.Equals(path, PrivateVarPath, StringComparison.Ordinal))
        {
            return VarPath;
        }

        const string privateVarPrefix = PrivateVarPath + "/";
        return path.StartsWith(privateVarPrefix, StringComparison.Ordinal)
            ? VarPath + "/" + path[privateVarPrefix.Length..]
            : path;
    }
}
