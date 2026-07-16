namespace MackySoft.Tests;

internal static class CallCommandPreflightAssert
{
    public static RecordingCallCommandPreflightService.Invocation PreparedOnce (
        RecordingCallCommandPreflightService preflightService,
        string? expectedProjectPath,
        string expectedRequestJson)
    {
        var invocation = Assert.Single(preflightService.Invocations);
        Assert.NotEqual(Guid.Empty, invocation.RequestId);
        Assert.Equal(expectedProjectPath, invocation.ProjectPath);
        Assert.Equal(expectedRequestJson, invocation.RequestJson);
        return invocation;
    }
}
