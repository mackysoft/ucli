using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class OperationExecuteServiceInvocationAssert
{
    public static RecordingOperationExecuteService.Invocation ExecutedOnce (
        RecordingOperationExecuteService executeService,
        UcliCommand expectedCommand,
        string expectedOperationId,
        string expectedOperationName,
        UcliOperationKind expectedKind,
        OperationPolicy expectedPolicy,
        string expectedSuccessMessage,
        string expectedFailureMessage,
        string expectedProjectPath,
        UnityExecutionMode? expectedMode,
        int? expectedTimeoutMilliseconds,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(executeService.Invocations);
        AssertDefinition(
            invocation.Definition,
            expectedCommand,
            expectedOperationId,
            expectedOperationName,
            expectedKind,
            expectedPolicy,
            expectedSuccessMessage,
            expectedFailureMessage);
        AssertInput(
            invocation.Input,
            expectedProjectPath,
            expectedMode,
            expectedTimeoutMilliseconds,
            expectedFailFast);
        return invocation;
    }

    private static void AssertDefinition (
        OperationExecuteDefinition definition,
        UcliCommand expectedCommand,
        string expectedOperationId,
        string expectedOperationName,
        UcliOperationKind expectedKind,
        OperationPolicy expectedPolicy,
        string expectedSuccessMessage,
        string expectedFailureMessage)
    {
        Assert.Equal(expectedCommand, definition.Command);
        Assert.Equal(expectedOperationId, definition.OperationId);
        Assert.Equal(expectedOperationName, definition.Descriptor.Name);
        Assert.Equal(expectedKind, definition.Descriptor.Kind);
        Assert.Equal(expectedPolicy, definition.Descriptor.Policy);
        Assert.Equal(JsonValueKind.Object, definition.Args.ValueKind);
        Assert.Equal(expectedSuccessMessage, definition.SuccessMessage);
        Assert.Equal(expectedFailureMessage, definition.FailureMessage);
    }

    private static void AssertInput (
        OperationExecuteInput input,
        string expectedProjectPath,
        UnityExecutionMode? expectedMode,
        int? expectedTimeoutMilliseconds,
        bool expectedFailFast)
    {
        Assert.Equal(expectedProjectPath, input.ProjectPath);
        Assert.Equal(expectedMode, input.Mode);
        Assert.Equal(expectedTimeoutMilliseconds, input.TimeoutMilliseconds);
        Assert.Equal(expectedFailFast, input.FailFast);
    }
}
