using System.Globalization;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode;

namespace MackySoft.Ucli.Features.Testing.Run.Artifacts;

/// <summary> Implements metadata JSON writing for test-run artifact sessions. </summary>
internal sealed class TestRunMetaStore : ITestRunMetaStore
{
    private const int MetaSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Writes one metadata snapshot for a run session. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="session"> The artifacts session. </param>
    /// <param name="finishedAtUtc"> The completion timestamp to persist. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes when metadata writing is finished. </returns>
    public async ValueTask WriteAsync (
        ResolvedTestRunConfiguration configuration,
        ArtifactsSession session,
        DateTimeOffset finishedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(session);

        var payload = new MetaJsonPayload(
            SchemaVersion: MetaSchemaVersion,
            RunId: session.RunId,
            StartedAt: session.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            FinishedAt: finishedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ProjectPath: configuration.UnityProject.UnityProjectRoot.Value,
            UnityVersion: configuration.UnityVersion,
            UnityEditorPath: configuration.UnityEditorPath.Value,
            Mode: UnityExecutionModeCodec.ToValue(configuration.Mode),
            TestPlatform: TestRunPlatformCodec.ToValue(configuration.TestPlatform),
            TestFilter: configuration.TestFilter,
            TestCategories: configuration.TestCategories,
            AssemblyNames: configuration.AssemblyNames,
            ArtifactsDir: session.Paths.ArtifactsDir.Value);

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        await File.WriteAllTextAsync(session.Paths.MetaJsonPath.Value, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Represents metadata payload for one test-run artifacts session. </summary>
    /// <param name="SchemaVersion"> The metadata schema version. </param>
    /// <param name="RunId"> The run identifier. </param>
    /// <param name="StartedAt"> The run start timestamp in ISO-8601 UTC format. </param>
    /// <param name="FinishedAt"> The run completion timestamp in ISO-8601 UTC format. </param>
    /// <param name="ProjectPath"> The Unity project path. </param>
    /// <param name="UnityVersion"> The resolved Unity version. </param>
    /// <param name="UnityEditorPath"> The resolved Unity editor path. </param>
    /// <param name="Mode"> The execution mode option value. </param>
    /// <param name="TestPlatform"> The test-platform value. </param>
    /// <param name="TestFilter"> The optional test-filter value. </param>
    /// <param name="TestCategories"> The normalized test-category values. </param>
    /// <param name="AssemblyNames"> The normalized assembly-name values. </param>
    /// <param name="ArtifactsDir"> The run artifacts directory path. </param>
    private sealed record MetaJsonPayload (
        int SchemaVersion,
        Guid RunId,
        string StartedAt,
        string FinishedAt,
        string ProjectPath,
        string UnityVersion,
        string UnityEditorPath,
        string Mode,
        string TestPlatform,
        string? TestFilter,
        string[] TestCategories,
        string[] AssemblyNames,
        string ArtifactsDir);
}
