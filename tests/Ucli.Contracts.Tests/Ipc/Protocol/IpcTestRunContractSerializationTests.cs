using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcTestRunContractSerializationTests
{
    private const string RunIdText = "12345678-1234-5678-9abc-def012345678";
    private static readonly Guid RunId = Guid.Parse(RunIdText);

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunProgressContracts_SerializeWithCamelCaseFields ()
    {
        var started = IpcPayloadCodec.SerializeToElement(
            new TestCaseStartedEntry(
                RunId,
                "test-1",
                "CanPass",
                "Assembly.Tests",
                "editmode",
                ["smoke"]));
        var finished = IpcPayloadCodec.SerializeToElement(
            new TestCaseFinishedEntry(
                RunId,
                "test-1",
                "CanPass",
                "Assembly.Tests",
                "editmode",
                ["smoke"],
                "pass",
                12,
                Message: null,
                StackTrace: null));
        var diagnostic = IpcPayloadCodec.SerializeToElement(
            new TestRunDiagnosticEntry(
                RunId,
                "TEST_PROGRESS_DROPPED",
                "Some progress entries were dropped.",
                "warning"));

        JsonAssert.For(started)
            .HasString("runId", RunIdText)
            .HasString("testId", "test-1")
            .HasString("testName", "CanPass")
            .HasString("assemblyName", "Assembly.Tests")
            .HasString("testPlatform", "editmode")
            .HasArrayLength("categories", 1);
        JsonAssert.For(finished)
            .HasString("result", "pass")
            .HasInt32("durationMilliseconds", 12)
            .HasValueKind("message", JsonValueKind.Null)
            .HasValueKind("stackTrace", JsonValueKind.Null);
        JsonAssert.For(diagnostic)
            .HasString("code", "TEST_PROGRESS_DROPPED")
            .HasString("message", "Some progress entries were dropped.")
            .HasString("severity", "warning");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcTestRunRequest(
            TestPlatform: "editmode",
            TestFilter: null,
            TestCategories: Array.Empty<string>(),
            AssemblyNames: Array.Empty<string>(),
            TestSettingsPath: null,
            ResultsXmlPath: "/tmp/results.xml",
            EditorLogPath: "/tmp/editor.log",
            RunId: RunId,
            FailFast: true);
        var responsePayload = new IpcTestRunResponse(ExitCode: 2);

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasString("testPlatform", "editmode")
            .IsNull("testFilter")
            .HasArrayLength("testCategories", 0)
            .HasArrayLength("assemblyNames", 0)
            .IsNull("testSettingsPath")
            .HasString("resultsXmlPath", "/tmp/results.xml")
            .HasString("editorLogPath", "/tmp/editor.log")
            .HasString("runId", RunIdText)
            .HasBoolean("failFast", true);
        JsonAssert.For(response)
            .HasInt32("exitCode", 2);
    }

}
