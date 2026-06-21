namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents build evidence grouped under <c>payload.build</c>. </summary>
internal sealed record BuildOutput (
    string RunId,
    BuildProfileOutput Profile,
    string BuildTarget,
    BuildScenesOutput Scenes,
    BuildOptionsOutput Options,
    BuildRunnerOutput Runner,
    BuildRunnerResultOutput RunnerResult,
    BuildArtifactOutput Output,
    BuildGenerationsOutput Generations,
    BuildSummaryOutput Summary,
    BuildLogsOutput Logs);
