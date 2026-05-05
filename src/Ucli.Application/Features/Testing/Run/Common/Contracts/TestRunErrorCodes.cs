namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

/// <summary> Defines machine-readable error codes used by test-run service results. </summary>
internal static class TestRunErrorCodes
{
    /// <summary> Gets the error code emitted when Unity test process execution fails. </summary>
    public const string UnityTestExecutionFailed = "UNITY_TEST_EXECUTION_FAILED";

    /// <summary> Gets the error code emitted when Unity test execution exceeds its runtime budget. </summary>
    public const string UnityTestExecutionTimeout = "UNITY_TEST_EXECUTION_TIMEOUT";

    /// <summary> Gets the error code emitted when Unity test results XML is invalid. </summary>
    public const string TestResultsXmlInvalid = "TEST_RESULTS_XML_INVALID";

    /// <summary> Gets the error code emitted when reading Unity test results XML fails. </summary>
    public const string TestResultsXmlReadFailed = "TEST_RESULTS_XML_READ_FAILED";

    /// <summary> Gets the error code emitted when writing normalized test result artifacts fails. </summary>
    public const string TestResultsOutputWriteFailed = "TEST_RESULTS_OUTPUT_WRITE_FAILED";
}
