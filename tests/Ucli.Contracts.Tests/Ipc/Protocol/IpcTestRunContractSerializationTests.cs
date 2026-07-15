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
                TestCaseResult.Pass,
                12,
                Message: null,
                StackTrace: null));
        var diagnostic = IpcPayloadCodec.SerializeToElement(
            new TestRunDiagnosticEntry(
                RunId,
                new UcliCode("TEST_PROGRESS_DROPPED"),
                "Some progress entries were dropped.",
                UcliDiagnosticSeverity.Warning));

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
            .HasString("runId", RunIdText)
            .HasBoolean("failFast", true);
        Assert.False(request.TryGetProperty("testSettingsPath", out _));
        Assert.False(request.TryGetProperty("resultsXmlPath", out _));
        Assert.False(request.TryGetProperty("editorLogPath", out _));
        JsonAssert.For(response)
            .HasInt32("exitCode", 2);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunRequest_WhenFailFastIsMissing_DeserializesAsFalse ()
    {
        var request = JsonSerializer.Deserialize<IpcTestRunRequest>(
            $$"""
            {
              "testPlatform": "editmode",
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "runId": "{{RunIdText}}"
            }
            """,
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(request);
        Assert.False(request.FailFast);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [Trait("Size", "Small")]
    public void IpcTestRunRequest_WhenFilterCollectionIsNull_ThrowsArgumentNullException (
        bool nullTestCategories,
        bool nullAssemblyNames)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcTestRunRequest(
            TestPlatform: "editmode",
            TestFilter: null,
            TestCategories: nullTestCategories ? null! : Array.Empty<string>(),
            AssemblyNames: nullAssemblyNames ? null! : Array.Empty<string>(),
            RunId: RunId,
            FailFast: false));

        Assert.Equal(nullTestCategories ? "TestCategories" : "AssemblyNames", exception.ParamName);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, " ")]
    [Trait("Size", "Small")]
    public void IpcTestRunRequest_WhenFilterCollectionContainsInvalidValue_ThrowsArgumentException (
        bool invalidTestCategories,
        string? invalidValue)
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcTestRunRequest(
            TestPlatform: "editmode",
            TestFilter: null,
            TestCategories: invalidTestCategories ? [invalidValue!] : Array.Empty<string>(),
            AssemblyNames: invalidTestCategories ? Array.Empty<string>() : [invalidValue!],
            RunId: RunId,
            FailFast: false));

        Assert.Equal(invalidTestCategories ? "TestCategories" : "AssemblyNames", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunRequest_CopiesFilterCollectionsIntoReadOnlySnapshots ()
    {
        var testCategories = new[] { "smoke" };
        var assemblyNames = new[] { "Assembly.Tests" };
        var request = new IpcTestRunRequest(
            TestPlatform: "editmode",
            TestFilter: null,
            TestCategories: testCategories,
            AssemblyNames: assemblyNames,
            RunId: RunId,
            FailFast: false);

        testCategories[0] = "changed";
        assemblyNames[0] = "Changed.Tests";

        Assert.Equal("smoke", Assert.Single(request.TestCategories));
        Assert.Equal("Assembly.Tests", Assert.Single(request.AssemblyNames));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)request.TestCategories)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)request.AssemblyNames)[0] = "Changed.Tests");
    }

    [Theory]
    [InlineData("testSettingsPath")]
    [InlineData("resultsXmlPath")]
    [InlineData("editorLogPath")]
    [Trait("Size", "Small")]
    public void IpcTestRunRequest_WhenRemovedClientControlledPropertyIsPresent_RejectsPayload (string propertyName)
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "testPlatform": "editmode",
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "runId": "{{RunIdText}}",
              "failFast": false,
              "{{propertyName}}": "/tmp/client-controlled"
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<IpcTestRunRequest>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestCaseFinished_WhenResultIsUnsupported_RejectsPayload ()
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "runId": "{{RunIdText}}",
              "testId": "test-1",
              "testName": "CanPass",
              "assemblyName": "Assembly.Tests",
              "testPlatform": "editmode",
              "categories": ["smoke"],
              "result": "unknown",
              "durationMilliseconds": 12,
              "message": null,
              "stackTrace": null
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<TestCaseFinishedEntry>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunDiagnostic_WhenSeverityIsUnsupported_RejectsPayload ()
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "runId": "{{RunIdText}}",
              "code": "TEST_DIAGNOSTIC",
              "message": "A diagnostic message.",
              "severity": "fatal"
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<TestRunDiagnosticEntry>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Theory]
    [InlineData("code", "invalid")]
    [InlineData("message", " ")]
    [Trait("Size", "Small")]
    public void IpcTestRunDiagnostic_WhenRequiredTextFieldIsInvalid_RejectsPayload (
        string propertyName,
        string invalidValue)
    {
        var code = propertyName == "code" ? invalidValue : "TEST_DIAGNOSTIC";
        var message = propertyName == "message" ? invalidValue : "A diagnostic message.";
        using var document = JsonDocument.Parse(
            $$"""
            {
              "runId": "{{RunIdText}}",
              "code": "{{code}}",
              "message": "{{message}}",
              "severity": "warning"
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<TestRunDiagnosticEntry>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }
}
