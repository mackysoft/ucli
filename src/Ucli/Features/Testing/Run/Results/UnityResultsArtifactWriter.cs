using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;

namespace MackySoft.Ucli.Features.Testing.Run.Results;

/// <summary> Implements writing normalized Unity test result artifacts. </summary>
internal sealed class UnityResultsArtifactWriter : IUnityResultsArtifactWriter
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Writes one results session artifacts from parsed XML values. </summary>
    /// <param name="session"> The run artifacts session. </param>
    /// <param name="parseResult"> The parsed XML result values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes when writing is finished. </returns>
    public async ValueTask WriteAsync (
        ArtifactsSession session,
        UnityResultsXmlParseResult parseResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(parseResult);

        cancellationToken.ThrowIfCancellationRequested();

        var resultsJsonPayload = new ResultsJsonPayload(
            SchemaVersion: SchemaVersion,
            RunId: session.RunId,
            Counts: parseResult.Counts,
            Tests: parseResult.Tests);
        var summaryJsonPayload = new SummaryJsonPayload(
            SchemaVersion: SchemaVersion,
            RunId: session.RunId,
            Status: parseResult.HasFailedTests ? "fail" : "pass",
            Counts: parseResult.Counts,
            TopFailures: parseResult.TopFailures);

        await WriteJsonAsync(session.Paths.ResultsJsonPath, resultsJsonPayload, cancellationToken).ConfigureAwait(false);
        await WriteJsonAsync(session.Paths.SummaryJsonPath, summaryJsonPayload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Writes one JSON payload to file. </summary>
    /// <typeparam name="TPayload"> The payload type. </typeparam>
    /// <param name="path"> The output path. </param>
    /// <param name="payload"> The output payload. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    private static Task WriteJsonAsync<TPayload> (
        AbsolutePath path,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return File.WriteAllTextAsync(path.Value, json, cancellationToken);
    }

    /// <summary> Represents schema-compliant <c>results.json</c> payload values. </summary>
    /// <param name="SchemaVersion"> The schema version. </param>
    /// <param name="RunId"> The run identifier. </param>
    /// <param name="Counts"> The aggregated counts values. </param>
    /// <param name="Tests"> The per-test entries. </param>
    private sealed record ResultsJsonPayload (
        int SchemaVersion,
        Guid RunId,
        UnityResultsXmlParseResult.CountsValue Counts,
        IReadOnlyList<UnityResultsXmlParseResult.TestValue> Tests);

    /// <summary> Represents schema-compliant <c>summary.json</c> payload values. </summary>
    /// <param name="SchemaVersion"> The schema version. </param>
    /// <param name="RunId"> The run identifier. </param>
    /// <param name="Status"> The overall result status. </param>
    /// <param name="Counts"> The aggregated counts values. </param>
    /// <param name="TopFailures"> The top failure entries. </param>
    private sealed record SummaryJsonPayload (
        int SchemaVersion,
        Guid RunId,
        string Status,
        UnityResultsXmlParseResult.CountsValue Counts,
        IReadOnlyList<UnityResultsXmlParseResult.TopFailureValue> TopFailures);
}
