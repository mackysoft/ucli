using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Tests;

namespace MackySoft.Tests;

internal static class OpsCommandAssert
{
    public static void ListOptionsDispatchedAsOpsQuery (
        RecordingOpsService service,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        string expectedNameRegex,
        UcliOperationKind expectedOperationKind,
        OperationPolicy expectedMaxPolicy,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(service.ListInvocations);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedReadIndexMode, invocation.Input.ReadIndexMode);
        Assert.Equal(expectedNameRegex, invocation.Input.NameRegex);
        Assert.Equal(expectedOperationKind, invocation.Input.Kind);
        Assert.Equal(expectedMaxPolicy, invocation.Input.MaxPolicy);
        Assert.Equal(expectedFailFast, invocation.Input.FailFast);
    }

    public static void DescribeOptionsDispatchedAsOpsQuery (
        RecordingOpsService service,
        string expectedOperationName,
        string expectedProjectPath,
        UnityExecutionMode expectedMode,
        int expectedTimeoutMilliseconds,
        ReadIndexMode expectedReadIndexMode,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(service.DescribeInvocations);
        Assert.Equal(expectedOperationName, invocation.Input.OperationName);
        Assert.Equal(expectedProjectPath, invocation.Input.ProjectPath);
        Assert.Equal(expectedMode, invocation.Input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        Assert.Equal(expectedReadIndexMode, invocation.Input.ReadIndexMode);
        Assert.Equal(expectedFailFast, invocation.Input.FailFast);
    }

    public static void InvalidListInputRejectedBeforeOpsExecution (
        CommandExecutionResult result,
        RecordingOpsService service,
        string goldenFileName)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.ListInvocations,
            UcliCommandNames.OpsList);

        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", goldenFileName),
            result.StdOut);
    }
}
