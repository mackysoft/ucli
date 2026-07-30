using System.Globalization;
using System.Xml.Linq;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Features.Testing.Run.Results;

/// <summary> Implements parsing for Unity test results XML files. </summary>
internal sealed class UnityResultsXmlParser : IUnityResultsXmlParser
{
    private const string TestRunElementName = "test-run";

    private const string TestCaseElementName = "test-case";

    private const string TestSuiteElementName = "test-suite";

    private const string ResultAttributeName = "result";

    private const string TotalAttributeName = "total";

    private const string PassedAttributeName = "passed";

    private const string FailedAttributeName = "failed";

    private const string SkippedAttributeName = "skipped";

    private const string InconclusiveAttributeName = "inconclusive";

    private const string TestCaseCountAttributeName = "testcasecount";

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
    public async ValueTask<UnityResultsXmlParseResult> ParseAsync (
        AbsolutePath resultsXmlPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resultsXmlPath);

        var xml = await File.ReadAllTextAsync(resultsXmlPath.Value, cancellationToken).ConfigureAwait(false);
        var document = XDocument.Parse(xml);

        var root = document.Root;
        if (root is null || !IsElement(root, TestRunElementName))
        {
            throw new InvalidDataException($"results.xml root must be <{TestRunElementName}>.");
        }

        var tests = new List<UnityResultsXmlParseResult.TestValue>();

        foreach (var testCase in root.Descendants().Where(static element => IsElement(element, TestCaseElementName)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullName = ReadRequiredAttribute(testCase, "fullname");
            var resultValue = ReadRequiredAttribute(testCase, "result");
            var durationValue = ReadRequiredAttribute(testCase, "duration");
            var result = ConvertResult(resultValue);
            var durationMilliseconds = ParseDurationMilliseconds(durationValue);
            var categories = ReadCategories(testCase);

            if (result == TestCaseResult.Fail)
            {
                var failureElement = testCase.Elements().FirstOrDefault(static element => IsElement(element, FailureElementName));
                var failureMessage = ReadChildElementText(failureElement, MessageElementName);
                var failureStackTrace = ReadChildElementText(failureElement, StackTraceElementName);

                tests.Add(UnityResultsXmlParseResult.TestValue.Failed(
                    fullName,
                    durationMilliseconds,
                    categories,
                    failureMessage,
                    failureStackTrace));
                continue;
            }

            tests.Add(CreateNonFailureTestValue(
                fullName,
                result,
                durationMilliseconds,
                categories));
        }

        var parseResult = UnityResultsXmlParseResult.Create(tests);
        ValidateReportedAggregates(root, parseResult.Counts);
        ValidateContainerResults(root);

        return parseResult;
    }

    private static UnityResultsXmlParseResult.TestValue CreateNonFailureTestValue (
        string fullName,
        TestCaseResult result,
        int durationMilliseconds,
        IReadOnlyList<string> categories)
    {
        return result switch
        {
            TestCaseResult.Pass => UnityResultsXmlParseResult.TestValue.Passed(
                fullName,
                durationMilliseconds,
                categories),
            TestCaseResult.Skipped => UnityResultsXmlParseResult.TestValue.Skipped(
                fullName,
                durationMilliseconds,
                categories),
            TestCaseResult.Inconclusive => UnityResultsXmlParseResult.TestValue.Inconclusive(
                fullName,
                durationMilliseconds,
                categories),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result,
                "Non-failure test result must be pass, skipped, or inconclusive."),
        };
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

    /// <summary> Converts Unity result attribute values to the public test-case result vocabulary. </summary>
    /// <param name="resultValue"> The raw result value. </param>
    /// <returns> The normalized test-case result. </returns>
    private static TestCaseResult ConvertResult (string resultValue)
    {
        var normalizedResultValue = resultValue.Trim();

        if (string.Equals(normalizedResultValue, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            return TestCaseResult.Pass;
        }

        if (string.Equals(normalizedResultValue, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return TestCaseResult.Fail;
        }

        if (string.Equals(normalizedResultValue, "Skipped", StringComparison.OrdinalIgnoreCase))
        {
            return TestCaseResult.Skipped;
        }

        if (string.Equals(normalizedResultValue, "Inconclusive", StringComparison.OrdinalIgnoreCase))
        {
            return TestCaseResult.Inconclusive;
        }

        throw new InvalidDataException($"result attribute contains an unsupported Unity test result: {resultValue}");
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

    /// <summary> Validates optional NUnit aggregate attributes against the normalized case collection. </summary>
    private static void ValidateReportedAggregates (
        XElement root,
        UnityResultsXmlParseResult.CountsValue counts)
    {
        var totalText = root.Attribute(TotalAttributeName)?.Value;
        var passedText = root.Attribute(PassedAttributeName)?.Value;
        var failedText = root.Attribute(FailedAttributeName)?.Value;
        var skippedText = root.Attribute(SkippedAttributeName)?.Value;
        var inconclusiveText = root.Attribute(InconclusiveAttributeName)?.Value;
        var aggregateValues = new[]
        {
            totalText,
            passedText,
            failedText,
            skippedText,
            inconclusiveText,
        };
        var presentAggregateCount = aggregateValues.Count(static value => value is not null);
        if (presentAggregateCount != 0 && presentAggregateCount != aggregateValues.Length)
        {
            throw new InvalidDataException(
                "test-run aggregate attributes must define total, passed, failed, skipped, and inconclusive together.");
        }

        var derivedTotal = checked(counts.Passed + counts.Failed + counts.Skipped + counts.Inconclusive);
        if (presentAggregateCount == aggregateValues.Length)
        {
            var reportedTotal = ParseNonNegativeInteger(totalText!, TotalAttributeName);
            var reportedPassed = ParseNonNegativeInteger(passedText!, PassedAttributeName);
            var reportedFailed = ParseNonNegativeInteger(failedText!, FailedAttributeName);
            var reportedSkipped = ParseNonNegativeInteger(skippedText!, SkippedAttributeName);
            var reportedInconclusive = ParseNonNegativeInteger(inconclusiveText!, InconclusiveAttributeName);
            var reportedCategoryTotal =
                (long)reportedPassed + reportedFailed + reportedSkipped + reportedInconclusive;
            if (reportedTotal != reportedCategoryTotal
                || reportedTotal != derivedTotal
                || reportedPassed != counts.Passed
                || reportedFailed != counts.Failed
                || reportedSkipped != counts.Skipped
                || reportedInconclusive != counts.Inconclusive)
            {
                throw new InvalidDataException(
                    "test-run aggregate attributes do not match the normalized test-case collection.");
            }
        }

        var testCaseCountText = root.Attribute(TestCaseCountAttributeName)?.Value;
        if (testCaseCountText is not null
            && ParseNonNegativeInteger(testCaseCountText, TestCaseCountAttributeName) != derivedTotal)
        {
            throw new InvalidDataException(
                "test-run testcasecount does not match the normalized test-case collection.");
        }
    }

    /// <summary> Validates that container result attributes are grounded in descendant case results. </summary>
    private static void ValidateContainerResults (XElement root)
    {
        foreach (var container in root
                     .DescendantsAndSelf()
                     .Where(static element => IsElement(element, TestRunElementName)
                         || IsElement(element, TestSuiteElementName)))
        {
            var resultValue = container.Attribute(ResultAttributeName)?.Value;
            if (string.IsNullOrWhiteSpace(resultValue))
            {
                continue;
            }

            var containerResult = ConvertResult(resultValue);
            var descendantResults = container
                .Descendants()
                .Where(static element => IsElement(element, TestCaseElementName))
                .Select(static element => ConvertResult(ReadRequiredAttribute(element, ResultAttributeName)))
                .ToArray();
            var isGrounded = containerResult switch
            {
                TestCaseResult.Pass => !descendantResults.Contains(TestCaseResult.Fail),
                TestCaseResult.Fail => descendantResults.Contains(TestCaseResult.Fail),
                TestCaseResult.Skipped => descendantResults.Contains(TestCaseResult.Skipped),
                TestCaseResult.Inconclusive => descendantResults.Contains(TestCaseResult.Inconclusive)
                    || descendantResults.Contains(TestCaseResult.Skipped),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(containerResult),
                    containerResult,
                    "Container result must be a defined test-case result."),
            };
            if (!isGrounded)
            {
                throw new InvalidDataException(
                    $"{container.Name.LocalName} result '{resultValue}' is not supported by its test-case collection.");
            }
        }
    }

    private static int ParseNonNegativeInteger (
        string value,
        string attributeName)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedValue))
        {
            throw new InvalidDataException(
                $"test-run '{attributeName}' attribute must be a non-negative Int32 value.");
        }

        return parsedValue;
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
