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
        Converters =
        {
            new VocabularyJsonConverterFactory(),
        },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Writes one results session from the complete normalized verdict evaluation. </summary>
    /// <param name="session"> The run artifacts session. </param>
    /// <param name="verdictEvaluation"> The normalized result, policy input, and verdict derived from that result. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that completes when writing is finished. </returns>
    public async ValueTask WriteAsync (
        ArtifactsSession session,
        TestRunVerdictEvaluation verdictEvaluation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(verdictEvaluation);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedResult = verdictEvaluation.NormalizedResult;
        var resultsJsonPayload = new ResultsJsonPayload(
            SchemaVersion: SchemaVersion,
            RunId: session.RunId,
            Counts: normalizedResult.Counts,
            Tests: normalizedResult.Tests);
        var summaryJsonPayload = new SummaryJsonPayload(
            SchemaVersion: SchemaVersion,
            RunId: session.RunId,
            Verdict: verdictEvaluation.Verdict,
            AllowEmptyTestRun: verdictEvaluation.AllowEmptyTestRun,
            Counts: normalizedResult.Counts,
            TopFailures: normalizedResult.TopFailures);

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
    /// <param name="Verdict"> The verdict derived from the complete normalized test result. </param>
    /// <param name="AllowEmptyTestRun"> Whether an empty result set satisfies the requested test condition. </param>
    /// <param name="Counts"> The aggregated counts values. </param>
    /// <param name="TopFailures"> The top failure entries. </param>
    private sealed record SummaryJsonPayload (
        int SchemaVersion,
        Guid RunId,
        Verdict Verdict,
        bool AllowEmptyTestRun,
        UnityResultsXmlParseResult.CountsValue Counts,
        IReadOnlyList<UnityResultsXmlParseResult.TopFailureValue> TopFailures);
}
