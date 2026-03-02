using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using MackySoft.Ucli.TestRun.Artifacts;

namespace MackySoft.Ucli.TestRun.Results;

/// <summary> Implements conversion from Unity test result XML into normalized JSON artifacts. </summary>
internal sealed class UnityResultsConverter : IUnityResultsConverter
{
    private const int SchemaVersion = 1;

    private const int MaxTopFailures = 10;

    private const string TestRunElementName = "test-run";

    private const string TestCaseElementName = "test-case";

    private const string FailureElementName = "failure";

    private const string MessageElementName = "message";

    private const string StackTraceElementName = "stack-trace";

    private const string PropertiesElementName = "properties";

    private const string PropertyElementName = "property";

    private const string CategoryPropertyName = "Category";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Converts one artifacts session results XML into normalized JSON artifacts. </summary>
    /// <param name="session"> The run artifacts session. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the conversion result. </returns>
    public async ValueTask<UnityResultsConversionResult> Convert (
        ArtifactsSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        ParseResult parseResult;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            parseResult = await ParseResultsXmlAsync(session.Paths.ResultsXmlPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.Canceled,
                "Unity results conversion was canceled.");
        }
        catch (Exception exception) when (IsInvalidResultsXmlException(exception))
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.InvalidResultsXml,
                $"Failed to parse results.xml: {exception.Message}");
        }

        var resultsJsonPayload = new ResultsJsonPayload(
            SchemaVersion: SchemaVersion,
            RunId: session.RunId,
            Counts: parseResult.Counts,
            Tests: parseResult.Tests);
        var summaryJsonPayload = new SummaryJsonPayload(
            SchemaVersion: SchemaVersion,
            RunId: session.RunId,
            Status: parseResult.Counts.Failed > 0 ? "fail" : "pass",
            Counts: parseResult.Counts,
            TopFailures: parseResult.TopFailures);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteJsonAsync(session.Paths.ResultsJsonPath, resultsJsonPayload, cancellationToken).ConfigureAwait(false);
            await WriteJsonAsync(session.Paths.SummaryJsonPath, summaryJsonPayload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.Canceled,
                "Unity results conversion was canceled.");
        }
        catch (Exception exception) when (IsOutputWriteException(exception))
        {
            return UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.OutputWriteFailed,
                $"Failed to write results artifacts: {exception.Message}");
        }

        return UnityResultsConversionResult.Success(parseResult.Counts.Failed > 0);
    }

    /// <summary> Parses one Unity results XML file. </summary>
    /// <param name="resultsXmlPath"> The results XML path. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to parsed result values. </returns>
    private static async ValueTask<ParseResult> ParseResultsXmlAsync (
        string resultsXmlPath,
        CancellationToken cancellationToken)
    {
        var xml = await File.ReadAllTextAsync(resultsXmlPath, cancellationToken).ConfigureAwait(false);
        var document = XDocument.Parse(xml);

        var root = document.Root;
        if (root is null || !IsElement(root, TestRunElementName))
        {
            throw new InvalidDataException($"results.xml root must be <{TestRunElementName}>.");
        }

        var tests = new List<TestEntry>();
        var topFailures = new List<TopFailureEntry>();
        var counts = new Counts(0, 0, 0);

        foreach (var testCase in root.Descendants().Where(static element => IsElement(element, TestCaseElementName)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullName = ReadRequiredAttribute(testCase, "fullname");
            var resultValue = ReadRequiredAttribute(testCase, "result");
            var durationValue = ReadRequiredAttribute(testCase, "duration");
            var outcome = ConvertOutcome(resultValue);
            var durationMilliseconds = ParseDurationMilliseconds(durationValue);
            var categories = ReadCategories(testCase);

            tests.Add(new TestEntry(
                FullName: fullName,
                Outcome: outcome,
                DurationMs: durationMilliseconds,
                Categories: categories));

            counts = counts with
            {
                Passed = counts.Passed + (outcome == "passed" ? 1 : 0),
                Failed = counts.Failed + (outcome == "failed" ? 1 : 0),
                Skipped = counts.Skipped + (outcome == "skipped" ? 1 : 0),
            };

            if (outcome == "failed" && topFailures.Count < MaxTopFailures)
            {
                var failureElement = testCase.Elements().FirstOrDefault(static element => IsElement(element, FailureElementName));
                var failureMessage = ReadChildElementText(failureElement, MessageElementName);
                var failureStackTrace = ReadChildElementText(failureElement, StackTraceElementName);

                topFailures.Add(new TopFailureEntry(
                    FullName: fullName,
                    Message: failureMessage,
                    StackTrace: failureStackTrace));
            }
        }

        return new ParseResult(
            Counts: counts,
            Tests: tests,
            TopFailures: topFailures);
    }

    /// <summary> Writes one JSON payload to file. </summary>
    /// <typeparam name="TPayload"> The payload type. </typeparam>
    /// <param name="path"> The output path. </param>
    /// <param name="payload"> The output payload. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    private static Task WriteJsonAsync<TPayload> (
        string path,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <summary> Parses duration seconds string and converts to milliseconds. </summary>
    /// <param name="value"> The duration value in seconds. </param>
    /// <returns> The duration in milliseconds. </returns>
    private static int ParseDurationMilliseconds (string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            throw new InvalidDataException($"duration attribute is not a valid number: {value}");
        }

        if (seconds < 0d)
        {
            throw new InvalidDataException($"duration attribute must be non-negative: {value}");
        }

        var roundedMilliseconds = Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero);
        if (roundedMilliseconds > int.MaxValue)
        {
            throw new OverflowException($"duration in milliseconds exceeds Int32 range: {value}");
        }

        return (int)roundedMilliseconds;
    }

    /// <summary> Converts Unity result attribute values to normalized outcome values. </summary>
    /// <param name="resultValue"> The raw result value. </param>
    /// <returns> The normalized outcome value. </returns>
    private static string ConvertOutcome (string resultValue)
    {
        if (string.Equals(resultValue.Trim(), "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        if (string.Equals(resultValue.Trim(), "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "failed";
        }

        if (string.Equals(resultValue.Trim(), "Skipped", StringComparison.OrdinalIgnoreCase))
        {
            return "skipped";
        }

        return "failed";
    }

    /// <summary> Reads category values from one test-case element. </summary>
    /// <param name="testCase"> The test-case element. </param>
    /// <returns> The distinct category values preserving XML order. </returns>
    private static string[] ReadCategories (XElement testCase)
    {
        var propertiesElement = testCase.Elements().FirstOrDefault(static element => IsElement(element, PropertiesElementName));
        if (propertiesElement is null)
        {
            return Array.Empty<string>();
        }

        var categories = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var propertyElement in propertiesElement.Elements().Where(static element => IsElement(element, PropertyElementName)))
        {
            var propertyName = propertyElement.Attribute("name")?.Value;
            if (!string.Equals(propertyName, CategoryPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var categoryValue = propertyElement.Attribute("value")?.Value;
            if (string.IsNullOrWhiteSpace(categoryValue))
            {
                continue;
            }

            if (seen.Add(categoryValue))
            {
                categories.Add(categoryValue);
            }
        }

        return categories.ToArray();
    }

    /// <summary> Reads required non-empty attribute values from one element. </summary>
    /// <param name="element"> The source XML element. </param>
    /// <param name="attributeName"> The required attribute name. </param>
    /// <returns> The required attribute value. </returns>
    private static string ReadRequiredAttribute (
        XElement element,
        string attributeName)
    {
        var value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"test-case requires non-empty '{attributeName}' attribute.");
        }

        return value;
    }

    /// <summary> Reads child element text values. </summary>
    /// <param name="parent"> The parent XML element. </param>
    /// <param name="childName"> The child local name. </param>
    /// <returns> The child text when present; otherwise empty string. </returns>
    private static string ReadChildElementText (
        XElement? parent,
        string childName)
    {
        if (parent is null)
        {
            return string.Empty;
        }

        var child = parent.Elements().FirstOrDefault(element => IsElement(element, childName));
        return child?.Value ?? string.Empty;
    }

    /// <summary> Determines whether one element local name matches expected value. </summary>
    /// <param name="element"> The target XML element. </param>
    /// <param name="expectedLocalName"> The expected local name. </param>
    /// <returns> <see langword="true" /> when local names match; otherwise <see langword="false" />. </returns>
    private static bool IsElement (
        XElement element,
        string expectedLocalName)
    {
        return string.Equals(element.Name.LocalName, expectedLocalName, StringComparison.Ordinal);
    }

    /// <summary> Determines whether one exception represents invalid input XML. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception should map to invalid-results failure; otherwise <see langword="false" />. </returns>
    private static bool IsInvalidResultsXmlException (Exception exception)
    {
        return exception is XmlException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or OverflowException;
    }

    /// <summary> Determines whether one exception represents output write failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception should map to output-write failure; otherwise <see langword="false" />. </returns>
    private static bool IsOutputWriteException (Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    /// <summary> Represents parsed result values used to create output payloads. </summary>
    /// <param name="Counts"> The aggregated counts. </param>
    /// <param name="Tests"> The per-test entries. </param>
    /// <param name="TopFailures"> The top failure entries. </param>
    private sealed record ParseResult (
        Counts Counts,
        IReadOnlyList<TestEntry> Tests,
        IReadOnlyList<TopFailureEntry> TopFailures);

    /// <summary> Represents schema-compliant counts values. </summary>
    /// <param name="Passed"> The passed-test count. </param>
    /// <param name="Failed"> The failed-test count. </param>
    /// <param name="Skipped"> The skipped-test count. </param>
    private sealed record Counts (
        int Passed,
        int Failed,
        int Skipped);

    /// <summary> Represents schema-compliant <c>results.json</c> payload values. </summary>
    /// <param name="SchemaVersion"> The schema version. </param>
    /// <param name="RunId"> The run identifier. </param>
    /// <param name="Counts"> The aggregated counts values. </param>
    /// <param name="Tests"> The per-test entries. </param>
    private sealed record ResultsJsonPayload (
        int SchemaVersion,
        string RunId,
        Counts Counts,
        IReadOnlyList<TestEntry> Tests);

    /// <summary> Represents one per-test results entry. </summary>
    /// <param name="FullName"> The fully qualified test name. </param>
    /// <param name="Outcome"> The normalized outcome value. </param>
    /// <param name="DurationMs"> The duration in milliseconds. </param>
    /// <param name="Categories"> The category values. </param>
    private sealed record TestEntry (
        string FullName,
        string Outcome,
        int DurationMs,
        string[] Categories);

    /// <summary> Represents schema-compliant <c>summary.json</c> payload values. </summary>
    /// <param name="SchemaVersion"> The schema version. </param>
    /// <param name="RunId"> The run identifier. </param>
    /// <param name="Status"> The summary status value. </param>
    /// <param name="Counts"> The aggregated counts values. </param>
    /// <param name="TopFailures"> The top failure entries. </param>
    private sealed record SummaryJsonPayload (
        int SchemaVersion,
        string RunId,
        string Status,
        Counts Counts,
        IReadOnlyList<TopFailureEntry> TopFailures);

    /// <summary> Represents one top-failure entry. </summary>
    /// <param name="FullName"> The fully qualified test name. </param>
    /// <param name="Message"> The failure message text. </param>
    /// <param name="StackTrace"> The failure stack-trace text. </param>
    private sealed record TopFailureEntry (
        string FullName,
        string Message,
        string StackTrace);
}