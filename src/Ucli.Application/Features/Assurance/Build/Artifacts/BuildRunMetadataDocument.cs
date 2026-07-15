using System.Text.Json;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the non-artifact-reference fields persisted into <c>build.json</c>. </summary>
internal sealed record BuildRunMetadataDocument
{
    /// <summary> Initializes the metadata sections persisted for one identified build run. </summary>
    /// <param name="schemaVersion"> The build metadata schema version. </param>
    /// <param name="runId"> The non-empty build run identifier. </param>
    /// <param name="profile"> The profile metadata section. </param>
    /// <param name="inputs"> The resolved build inputs section. </param>
    /// <param name="runner"> The resolved runner metadata section. </param>
    /// <param name="runnerResult"> The normalized runner terminal result section. </param>
    /// <param name="lifecycle"> The lifecycle evidence section. </param>
    /// <param name="generations"> The Unity generation evidence section. </param>
    /// <param name="summary"> The build summary section. </param>
    /// <param name="logs"> The log reference metadata section. </param>
    /// <param name="projectMutation"> The project mutation audit section. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="runId" /> is empty. </exception>
    public BuildRunMetadataDocument (
        int schemaVersion,
        Guid runId,
        JsonElement profile,
        JsonElement inputs,
        JsonElement runner,
        JsonElement runnerResult,
        JsonElement lifecycle,
        JsonElement generations,
        JsonElement summary,
        JsonElement logs,
        JsonElement projectMutation)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(runId));
        }

        SchemaVersion = schemaVersion;
        RunId = runId;
        Profile = profile;
        Inputs = inputs;
        Runner = runner;
        RunnerResult = runnerResult;
        Lifecycle = lifecycle;
        Generations = generations;
        Summary = summary;
        Logs = logs;
        ProjectMutation = projectMutation;
    }

    public int SchemaVersion { get; }

    /// <summary> Gets the non-empty build run identifier. </summary>
    public Guid RunId { get; }

    public JsonElement Profile { get; }

    public JsonElement Inputs { get; }

    public JsonElement Runner { get; }

    public JsonElement RunnerResult { get; }

    public JsonElement Lifecycle { get; }

    public JsonElement Generations { get; }

    public JsonElement Summary { get; }

    public JsonElement Logs { get; }

    public JsonElement ProjectMutation { get; }
}
