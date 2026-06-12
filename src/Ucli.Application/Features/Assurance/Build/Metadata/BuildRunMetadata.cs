using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the persisted build metadata artifact. </summary>
internal sealed record BuildRunMetadata (
    int SchemaVersion,
    string RunId,
    ProjectIdentityInfo Project,
    BuildProfileOutput Profile,
    BuildRunInputMetadata Input,
    BuildGenerationsOutput Generations,
    BuildSummaryOutput Summary,
    BuildLogsOutput Logs,
    BuildArtifactOutput Output,
    IReadOnlyDictionary<string, BuildReportOutput> Artifacts,
    IpcBuildDirtyState DirtyState);
