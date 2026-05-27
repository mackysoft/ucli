namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

/// <summary> Defines machine-readable error codes used by test-run service results. </summary>
internal static class TestRunErrorCodes
{
    /// <summary> Gets the error code emitted when Unity test process execution fails. </summary>
    public static readonly UcliCode UnityTestExecutionFailed = new UcliCode("UNITY_TEST_EXECUTION_FAILED");

    /// <summary> Gets the error code emitted when Unity test execution exceeds its runtime budget. </summary>
    public static readonly UcliCode UnityTestExecutionTimeout = new UcliCode("UNITY_TEST_EXECUTION_TIMEOUT");

    /// <summary> Gets the error code emitted when a test run reports no executed test cases. </summary>
    public static readonly UcliCode TestRunNoTestsExecuted = new UcliCode("TEST_RUN_NO_TESTS_EXECUTED");

    /// <summary> Gets the error code emitted when Unity test results XML is invalid. </summary>
    public static readonly UcliCode TestResultsXmlInvalid = new UcliCode("TEST_RESULTS_XML_INVALID");

    /// <summary> Gets the error code emitted when reading Unity test results XML fails. </summary>
    public static readonly UcliCode TestResultsXmlReadFailed = new UcliCode("TEST_RESULTS_XML_READ_FAILED");

    /// <summary> Gets the error code emitted when writing normalized test result artifacts fails. </summary>
    public static readonly UcliCode TestResultsOutputWriteFailed = new UcliCode("TEST_RESULTS_OUTPUT_WRITE_FAILED");
}
