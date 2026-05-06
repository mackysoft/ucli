namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

/// <summary> Defines machine-readable error codes used by test-run service results. </summary>
internal static class TestRunErrorCodes
{
    /// <summary> Gets the error code emitted when Unity test process execution fails. </summary>
    public static readonly UcliErrorCode UnityTestExecutionFailed = new UcliErrorCode("UNITY_TEST_EXECUTION_FAILED");

    /// <summary> Gets the error code emitted when Unity test execution exceeds its runtime budget. </summary>
    public static readonly UcliErrorCode UnityTestExecutionTimeout = new UcliErrorCode("UNITY_TEST_EXECUTION_TIMEOUT");

    /// <summary> Gets the error code emitted when Unity test results XML is invalid. </summary>
    public static readonly UcliErrorCode TestResultsXmlInvalid = new UcliErrorCode("TEST_RESULTS_XML_INVALID");

    /// <summary> Gets the error code emitted when reading Unity test results XML fails. </summary>
    public static readonly UcliErrorCode TestResultsXmlReadFailed = new UcliErrorCode("TEST_RESULTS_XML_READ_FAILED");

    /// <summary> Gets the error code emitted when writing normalized test result artifacts fails. </summary>
    public static readonly UcliErrorCode TestResultsOutputWriteFailed = new UcliErrorCode("TEST_RESULTS_OUTPUT_WRITE_FAILED");
}
