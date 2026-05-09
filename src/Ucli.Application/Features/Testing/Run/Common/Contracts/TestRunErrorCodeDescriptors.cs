using MackySoft.Ucli.Application.Shared.Diagnostics;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

internal static class TestRunErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        ApplicationErrorCodeDescriptorFactory.Create(
            code: TestRunErrorCodes.UnityTestExecutionFailed,
            category: "testRun",
            summary: "Unity test process execution failed.",
            meaning: "The Unity test runner process exited unsuccessfully or could not complete the requested test execution.",
            appliesTo: [UcliCommandIds.TestRun],
            possiblePhases: ["testProcessExecution"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.testRun", "payload.artifacts", "logs unity"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect Unity test logs and result artifacts before rerunning the tests."),
            ],
            relatedCodes:
            [
                TestRunErrorCodes.UnityTestExecutionTimeout,
                TestRunErrorCodes.TestResultsXmlReadFailed,
            ]),

        ApplicationErrorCodeDescriptorFactory.Create(
            code: TestRunErrorCodes.UnityTestExecutionTimeout,
            category: "testRun",
            summary: "Unity test execution exceeded its runtime budget.",
            meaning: "The Unity test runner did not complete before the configured timeout elapsed.",
            appliesTo: [UcliCommandIds.TestRun],
            possiblePhases: ["testProcessExecution"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.testRun", "payload.artifacts", "logs unity"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect partial test output and Unity logs before deciding whether to rerun."),
            ],
            relatedCodes:
            [
                TestRunErrorCodes.UnityTestExecutionFailed,
                IpcTransportErrorCodes.IpcTimeout,
            ]),

        ApplicationErrorCodeDescriptorFactory.Create(
            code: TestRunErrorCodes.TestResultsXmlInvalid,
            category: "testRun",
            summary: "Unity test results XML is invalid.",
            meaning: "The test runner produced an XML result file that cannot be parsed as the expected contract.",
            appliesTo: [UcliCommandIds.TestRun],
            possiblePhases: ["testResultRead"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.artifacts", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect the raw test result artifact and Unity logs before rerunning."),
            ],
            relatedCodes: [TestRunErrorCodes.TestResultsXmlReadFailed]),

        ApplicationErrorCodeDescriptorFactory.Create(
            code: TestRunErrorCodes.TestResultsXmlReadFailed,
            category: "testRun",
            summary: "Reading Unity test results XML failed.",
            meaning: "uCLI could not read the test result artifact expected after Unity test execution.",
            appliesTo: [UcliCommandIds.TestRun],
            possiblePhases: ["testResultRead"],
            impliesNotApplied: false,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.artifacts", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Check whether the Unity test process produced the result file before rerunning."),
            ],
            relatedCodes:
            [
                TestRunErrorCodes.UnityTestExecutionFailed,
                TestRunErrorCodes.TestResultsXmlInvalid,
            ]),

        ApplicationErrorCodeDescriptorFactory.Create(
            code: TestRunErrorCodes.TestResultsOutputWriteFailed,
            category: "testRun",
            summary: "Writing normalized test result artifacts failed.",
            meaning: "uCLI could read test results but failed while writing normalized output artifacts.",
            appliesTo: [UcliCommandIds.TestRun],
            possiblePhases: ["testResultProjection"],
            impliesNotApplied: false,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.artifacts", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix the output path or filesystem permissions, then rerun if normalized artifacts are still needed."),
            ],
            relatedCodes: [TestRunErrorCodes.TestResultsXmlReadFailed]),
    ];
}
