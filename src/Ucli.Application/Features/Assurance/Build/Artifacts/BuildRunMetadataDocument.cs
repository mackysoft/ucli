using System.Text.Json;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the non-artifact-reference fields persisted into <c>build.json</c>. </summary>
/// <param name="SchemaVersion"> The build metadata schema version. </param>
/// <param name="RunId"> The build run identifier. </param>
/// <param name="Profile"> The profile metadata section. </param>
/// <param name="Inputs"> The resolved build inputs section. </param>
/// <param name="Runner"> The resolved runner metadata section. </param>
/// <param name="RunnerResult"> The normalized runner terminal result section. </param>
/// <param name="Lifecycle"> The lifecycle evidence section. </param>
/// <param name="Generations"> The Unity generation evidence section. </param>
/// <param name="Summary"> The build summary section. </param>
/// <param name="Logs"> The log reference metadata section. </param>
/// <param name="ProjectMutation"> The project mutation audit section. </param>
internal sealed record BuildRunMetadataDocument (
    int SchemaVersion,
    Guid RunId,
    JsonElement Profile,
    JsonElement Inputs,
    JsonElement Runner,
    JsonElement RunnerResult,
    JsonElement Lifecycle,
    JsonElement Generations,
    JsonElement Summary,
    JsonElement Logs,
    JsonElement ProjectMutation);
