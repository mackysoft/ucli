namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents build evidence grouped under <c>payload.build</c>. </summary>
internal sealed record BuildOutput (
    string RunId,
    BuildProfileOutput Profile,
    string Target,
    BuildScenesOutput Scenes,
    BuildOptionsOutput Options,
    BuildArtifactOutput Output,
    BuildGenerationsOutput Generations,
    BuildSummaryOutput Summary,
    BuildLogsOutput Logs);
