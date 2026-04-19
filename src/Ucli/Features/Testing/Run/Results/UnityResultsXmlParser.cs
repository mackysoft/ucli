using System.Globalization;
using System.Xml.Linq;

namespace MackySoft.Ucli.Features.Testing.Run.Results;

/// <summary> Implements parsing for Unity test results XML files. </summary>
internal sealed class UnityResultsXmlParser : IUnityResultsXmlParser
{
    private const int MaxTopFailures = 10;

    private const string TestRunElementName = "test-run";

    private const string TestCaseElementName = "test-case";

    private const string TestSuiteElementName = "test-suite";

    private const string ResultAttributeName = "result";

    private const string FailedResultValue = "Failed";

    private const string FailureElementName = "failure";

    private const string MessageElementName = "message";

    private const string StackTraceElementName = "stack-trace";

    private const string PropertiesElementName = "properties";

    private const string PropertyElementName = "property";

    private const string CategoryPropertyName = "Category";

    /// <summary> Parses one Unity test results XML file. </summary>
    /// <param name="resultsXmlPath"> The results XML path. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to parsed XML result values. </returns>
    public async ValueTask<UnityResultsXmlParseResult> Parse (
        string resultsXmlPath,
        CancellationToken cancellationToken = default)
    {
        var xml = await File.ReadAllTextAsync(resultsXmlPath, cancellationToken).ConfigureAwait(false);
        var document = XDocument.Parse(xml);

        var root = document.Root;
        if (root is null || !IsElement(root, TestRunElementName))
        {
            throw new InvalidDataException($"results.xml root must be <{TestRunElementName}>.");
        }

        var tests = new List<UnityResultsXmlParseResult.TestValue>();
        var topFailures = new List<UnityResultsXmlParseResult.TopFailureValue>();
        var counts = new UnityResultsXmlParseResult.CountsValue(0, 0, 0);

        foreach (var testCase in root.Descendants().Where(static element => IsElement(element, TestCaseElementName)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullName = ReadRequiredAttribute(testCase, "fullname");
            var resultValue = ReadRequiredAttribute(testCase, "result");
            var durationValue = ReadRequiredAttribute(testCase, "duration");
            var outcome = ConvertOutcome(resultValue);
            var durationMilliseconds = ParseDurationMilliseconds(durationValue);
            var categories = ReadCategories(testCase);

            tests.Add(new UnityResultsXmlParseResult.TestValue(
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

                topFailures.Add(new UnityResultsXmlParseResult.TopFailureValue(
                    FullName: fullName,
                    Message: failureMessage,
                    StackTrace: failureStackTrace));
            }
        }

        var hasSuiteFailure = DetectSuiteFailure(root);

        return new UnityResultsXmlParseResult(
            Counts: counts,
            Tests: tests,
            TopFailures: topFailures,
            HasSuiteFailure: hasSuiteFailure);
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

        if (!double.IsFinite(seconds))
        {
            throw new InvalidDataException($"duration attribute must be a finite number: {value}");
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
        var normalizedResultValue = resultValue.Trim();

        if (string.Equals(normalizedResultValue, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return "passed";
        }

        if (string.Equals(normalizedResultValue, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "failed";
        }

        if (string.Equals(normalizedResultValue, "Skipped", StringComparison.OrdinalIgnoreCase))
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

            if (seen.Add(categoryValue!))
            {
                categories.Add(categoryValue!);
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

        return value!;
    }

    /// <summary> Detects whether parsed XML includes failed suite-level result signals. </summary>
    /// <param name="root"> The parsed root element. </param>
    /// <returns> <see langword="true" /> when failed suite-level outcomes are present; otherwise <see langword="false" />. </returns>
    private static bool DetectSuiteFailure (XElement root)
    {
        if (HasFailedResultAttribute(root))
        {
            return true;
        }

        foreach (var suite in root.Descendants().Where(static element => IsElement(element, TestSuiteElementName)))
        {
            if (HasFailedResultAttribute(suite))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary> Determines whether one element carries a failed result attribute. </summary>
    /// <param name="element"> The source element. </param>
    /// <returns> <see langword="true" /> when result attribute equals <c>Failed</c>; otherwise <see langword="false" />. </returns>
    private static bool HasFailedResultAttribute (XElement element)
    {
        var resultValue = element.Attribute(ResultAttributeName)?.Value;
        if (string.IsNullOrWhiteSpace(resultValue))
        {
            return false;
        }

        return string.Equals(resultValue.Trim(), FailedResultValue, StringComparison.OrdinalIgnoreCase);
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
}