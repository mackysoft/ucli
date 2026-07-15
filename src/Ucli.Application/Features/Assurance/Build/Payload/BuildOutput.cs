namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents build evidence grouped under <c>payload.build</c>. </summary>
internal sealed record BuildOutput
{
    /// <summary> Initializes the complete evidence projection for one identified build run. </summary>
    /// <param name="runId"> The non-empty build run identifier. </param>
    /// <param name="profile"> The resolved build profile evidence. </param>
    /// <param name="inputs"> The resolved build input evidence. </param>
    /// <param name="runner"> The build runner invocation evidence. </param>
    /// <param name="runnerResult"> The normalized runner terminal result. </param>
    /// <param name="output"> The persisted build artifact evidence. </param>
    /// <param name="generations"> The Unity generation evidence. </param>
    /// <param name="summary"> The normalized build summary. </param>
    /// <param name="logs"> The build log evidence. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when an evidence section is <see langword="null" />. </exception>
    public BuildOutput (
        Guid runId,
        BuildProfileOutput profile,
        BuildInputsOutput inputs,
        BuildRunnerOutput runner,
        BuildRunnerResultOutput runnerResult,
        BuildArtifactOutput output,
        BuildGenerationsOutput generations,
        BuildSummaryOutput summary,
        BuildLogsOutput logs)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(runId));
        }

        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(runnerResult);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(generations);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(logs);

        RunId = runId;
        Profile = profile;
        Inputs = inputs;
        Runner = runner;
        RunnerResult = runnerResult;
        Output = output;
        Generations = generations;
        Summary = summary;
        Logs = logs;
    }

    /// <summary> Gets the non-empty build run identifier. </summary>
    public Guid RunId { get; }

    public BuildProfileOutput Profile { get; }

    public BuildInputsOutput Inputs { get; }

    public BuildRunnerOutput Runner { get; }

    public BuildRunnerResultOutput RunnerResult { get; }

    public BuildArtifactOutput Output { get; }

    public BuildGenerationsOutput Generations { get; }

    public BuildSummaryOutput Summary { get; }

    public BuildLogsOutput Logs { get; }
}
