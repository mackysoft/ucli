using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Tests;

internal static class ResolveCommandAssert
{
    public static void SucceededWithSceneHierarchySelector (
        CommandExecutionResult result,
        RecordingResolveService service,
        CancellationToken expectedCancellationToken,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast,
        string expectedScene,
        string expectedHierarchyPath)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.NotEqual(Guid.Empty, Assert.Single(service.RequestIds));
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedReadIndexMode, invocation.Input.ReadIndexMode);
        Assert.Equal(expectedFailFast, invocation.Input.FailFast);
        var selector = Assert.IsType<ResolveSceneHierarchySelectorInput>(invocation.Input.Selector);
        Assert.Equal(expectedScene, selector.Scene);
        Assert.Equal(expectedHierarchyPath, selector.HierarchyPath);
    }

    public static void SucceededWithPayload (
        CommandExecutionResult result,
        string expectedRequestId)
    {
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Resolve);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI resolve completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", expectedRequestId)
                .HasProperty("project", project => project
                    .HasString("projectPath", ProjectIdentityInfoTestFactory.DefaultProjectPath)
                    .HasString("projectFingerprint", ProjectIdentityInfoTestFactory.ProjectFingerprint.ToString())
                    .HasString("unityVersion", ProjectIdentityInfoTestFactory.UnityVersion))
                .HasArrayLength("opResults", 1)
                .HasProperty("opResults", 0, op => op
                    .HasString("opId", "resolve")
                    .HasString("op", UcliPrimitiveOperationNames.Resolve)
                    .HasString("phase", IpcExecuteOperationPhaseNames.Plan)
                    .HasBoolean("applied", false)
                    .HasBoolean("changed", false)
                    .HasProperty("result", result => result
                        .HasString("globalObjectId", "GlobalObjectId_V1-1-2-3-4-5-6")))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasString("source", "index")
                    .HasString("freshness", "fresh")));
    }

    public static void InvalidInputRejectedBeforeResolveExecution (
        CommandExecutionResult result,
        RecordingResolveService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.Resolve);
    }
}
