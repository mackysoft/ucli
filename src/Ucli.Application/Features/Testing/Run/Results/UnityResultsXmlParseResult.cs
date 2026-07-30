using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Represents parsed Unity results XML values used for JSON artifact generation. </summary>
internal sealed record UnityResultsXmlParseResult
{
    private const int MaxTopFailures = 10;

    private UnityResultsXmlParseResult (IReadOnlyList<TestValue> tests)
    {
        Tests = Array.AsReadOnly(tests.ToArray());
        Counts = CountsValue.FromTests(Tests);
        TopFailures = Array.AsReadOnly(Tests
            .Where(static test => test.Failure is not null)
            .Take(MaxTopFailures)
            .Select(static test => new TopFailureValue(
                test.FullName,
                test.Failure!.Message,
                test.Failure.StackTrace))
            .ToArray());
    }

    /// <summary> Gets count values derived from the normalized test collection. </summary>
    public CountsValue Counts { get; }

    /// <summary> Gets the normalized per-test entries. </summary>
    public IReadOnlyList<TestValue> Tests { get; }

    /// <summary> Gets the first failed tests in source order for summary output. </summary>
    public IReadOnlyList<TopFailureValue> TopFailures { get; }

    /// <summary> Gets the number of test cases reported by Unity results XML. </summary>
    public int ReportedTestCaseCount => Tests.Count;

    /// <summary> Creates a normalized result whose aggregate views are derived from one test collection. </summary>
    /// <param name="tests"> The normalized test entries in source order. </param>
    public static UnityResultsXmlParseResult Create (IReadOnlyList<TestValue> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        if (tests.Any(static test => test is null))
        {
            throw new ArgumentException("Normalized tests must not contain null.", nameof(tests));
        }

        return new UnityResultsXmlParseResult(tests);
    }

    /// <summary> Represents schema-compliant aggregated counts values. </summary>
    internal sealed record CountsValue
    {
        private CountsValue (
            int passed,
            int failed,
            int skipped,
            int inconclusive)
        {
            Passed = passed;
            Failed = failed;
            Skipped = skipped;
            Inconclusive = inconclusive;
        }

        public int Passed { get; }

        public int Failed { get; }

        public int Skipped { get; }

        public int Inconclusive { get; }

        internal static CountsValue FromTests (IReadOnlyList<TestValue> tests)
        {
            var passed = 0;
            var failed = 0;
            var skipped = 0;
            var inconclusive = 0;

            foreach (var test in tests)
            {
                checked
                {
                    switch (test.Outcome)
                    {
                        case TestCaseResult.Pass:
                            passed++;
                            break;
                        case TestCaseResult.Fail:
                            failed++;
                            break;
                        case TestCaseResult.Skipped:
                            skipped++;
                            break;
                        case TestCaseResult.Inconclusive:
                            inconclusive++;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(tests),
                                test.Outcome,
                                "Normalized test outcome must be defined.");
                    }
                }
            }

            return new CountsValue(passed, failed, skipped, inconclusive);
        }
    }

    /// <summary> Represents one per-test results entry. </summary>
    internal sealed record TestValue
    {
        private TestValue (
            string fullName,
            TestCaseResult outcome,
            int durationMs,
            IReadOnlyList<string> categories,
            FailureValue? failure)
        {
            FullName = string.IsNullOrWhiteSpace(fullName)
                ? throw new ArgumentException("Test full name must not be empty.", nameof(fullName))
                : fullName;
            if (durationMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Test duration must be non-negative.");
            }

            ArgumentNullException.ThrowIfNull(categories);
            if (categories.Any(static category => category is null))
            {
                throw new ArgumentException("Test categories must not contain null.", nameof(categories));
            }

            Outcome = outcome;
            DurationMs = durationMs;
            Categories = categories.ToArray();
            Failure = failure;
        }

        public string FullName { get; }

        public TestCaseResult Outcome { get; }

        public int DurationMs { get; }

        public string[] Categories { get; }

        internal FailureValue? Failure { get; }

        public static TestValue Passed (
            string fullName,
            int durationMs,
            IReadOnlyList<string> categories)
        {
            return new TestValue(
                fullName,
                TestCaseResult.Pass,
                durationMs,
                categories,
                failure: null);
        }

        public static TestValue Failed (
            string fullName,
            int durationMs,
            IReadOnlyList<string> categories,
            string message,
            string stackTrace)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(stackTrace);

            return new TestValue(
                fullName,
                TestCaseResult.Fail,
                durationMs,
                categories,
                new FailureValue(message, stackTrace));
        }

        public static TestValue Skipped (
            string fullName,
            int durationMs,
            IReadOnlyList<string> categories)
        {
            return new TestValue(
                fullName,
                TestCaseResult.Skipped,
                durationMs,
                categories,
                failure: null);
        }

        public static TestValue Inconclusive (
            string fullName,
            int durationMs,
            IReadOnlyList<string> categories)
        {
            return new TestValue(
                fullName,
                TestCaseResult.Inconclusive,
                durationMs,
                categories,
                failure: null);
        }

        internal sealed record FailureValue (
            string Message,
            string StackTrace);
    }

    /// <summary> Represents one top-failure entry for summary output. </summary>
    internal sealed record TopFailureValue
    {
        internal TopFailureValue (
            string fullName,
            string message,
            string stackTrace)
        {
            FullName = fullName;
            Message = message;
            StackTrace = stackTrace;
        }

        public string FullName { get; }

        public string Message { get; }

        public string StackTrace { get; }
    }
}
