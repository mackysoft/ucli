using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcCommonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKind_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)IpcTransportKind.NamedPipe);
        Assert.Equal(1, (int)IpcTransportKind.UnixDomainSocket);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcEndpoint_ConstructedValuesAreRetained ()
    {
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli.sock");

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.Equal("/tmp/ucli.sock", endpoint.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliRepositoryRoot", IpcDaemonBootstrapArgumentNames.RepositoryRoot);
        Assert.Equal("-ucliProjectFingerprint", IpcDaemonBootstrapArgumentNames.ProjectFingerprint);
        Assert.Equal("-ucliSessionPath", IpcDaemonBootstrapArgumentNames.SessionPath);
        Assert.Equal("-ucliEndpointTransportKind", IpcDaemonBootstrapArgumentNames.EndpointTransportKind);
        Assert.Equal("-ucliEndpointAddress", IpcDaemonBootstrapArgumentNames.EndpointAddress);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonBootstrapArgumentsCodec_HasStableUnityExecuteMethodName ()
    {
        Assert.Equal(
            "MackySoft.Ucli.Unity.Ipc.UnityDaemonBootstrap.Start",
            IpcDaemonBootstrapArgumentsCodec.UnityExecuteMethodName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcJsonSerializerOptions_Default_HasStableConfiguration ()
    {
        var options = IpcJsonSerializerOptions.Default;

        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.False(options.WriteIndented);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKindCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal(IpcTransportKindValues.NamedPipe, IpcTransportKindCodec.ToValue(IpcTransportKind.NamedPipe));
        Assert.Equal(IpcTransportKindValues.UnixDomainSocket, IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKindCodec_TryParse_AcceptsKnownValues ()
    {
        Assert.True(IpcTransportKindCodec.TryParse(IpcTransportKindValues.NamedPipe, out var namedPipe));
        Assert.Equal(IpcTransportKind.NamedPipe, namedPipe);
        Assert.True(IpcTransportKindCodec.TryParse(IpcTransportKindValues.UnixDomainSocket, out var unixDomainSocket));
        Assert.Equal(IpcTransportKind.UnixDomainSocket, unixDomainSocket);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKindCodec_TryParse_UnknownValue_ReturnsFalse ()
    {
        Assert.False(IpcTransportKindCodec.TryParse("unsupported", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileStateCodec_HasStableStringValues ()
    {
        Assert.Equal("ready", IpcCompileStateCodec.Ready);
        Assert.Equal("compiling", IpcCompileStateCodec.Compiling);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileStateCodec_ToValue_ReturnsExpectedLiterals ()
    {
        Assert.Equal(IpcCompileStateCodec.Ready, IpcCompileStateCodec.ToValue(false));
        Assert.Equal(IpcCompileStateCodec.Compiling, IpcCompileStateCodec.ToValue(true));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("ready", true, IpcCompileStateCodec.Ready)]
    [InlineData(" compiling ", true, IpcCompileStateCodec.Compiling)]
    [InlineData("READY", false, null)]
    [InlineData("unsupported", false, null)]
    [InlineData("", false, null)]
    [InlineData(" ", false, null)]
    [InlineData(null, false, null)]
    public void IpcCompileStateCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcCompileStateCodec.TryParse(value, out var compileState);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, compileState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsLevelCodec_HasStableStringValues ()
    {
        Assert.Equal("all", IpcDaemonLogsLevelCodec.All);
        Assert.Equal("error", IpcDaemonLogsLevelCodec.Error);
        Assert.Equal("warning", IpcDaemonLogsLevelCodec.Warning);
        Assert.Equal("info", IpcDaemonLogsLevelCodec.Info);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("all", true, IpcDaemonLogsLevelCodec.All)]
    [InlineData(" WARNING ", true, IpcDaemonLogsLevelCodec.Warning)]
    [InlineData("unsupported", false, null)]
    [InlineData("", false, null)]
    [InlineData(" ", false, null)]
    [InlineData(null, false, null)]
    public void IpcDaemonLogsLevelCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcDaemonLogsLevelCodec.TryParse(value, out var level);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, level);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsQueryTargetCodec_HasStableStringValues ()
    {
        Assert.Equal("message", IpcDaemonLogsQueryTargetCodec.Message);
        Assert.Equal("stack", IpcDaemonLogsQueryTargetCodec.Stack);
        Assert.Equal("both", IpcDaemonLogsQueryTargetCodec.Both);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("message", true, IpcDaemonLogsQueryTargetCodec.Message)]
    [InlineData(" STACK ", true, IpcDaemonLogsQueryTargetCodec.Stack)]
    [InlineData("both", true, IpcDaemonLogsQueryTargetCodec.Both)]
    [InlineData("unsupported", false, null)]
    [InlineData("", false, null)]
    [InlineData(" ", false, null)]
    [InlineData(null, false, null)]
    public void IpcDaemonLogsQueryTargetCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcDaemonLogsQueryTargetCodec.TryParse(value, out var queryTarget);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, queryTarget);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunPlatformCodec_HasStableStringValues ()
    {
        Assert.Equal("editmode", IpcTestRunPlatformCodec.EditMode);
        Assert.Equal("playmode", IpcTestRunPlatformCodec.PlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunPlatformCodec_HasStableUnityStringValues ()
    {
        Assert.Equal("EditMode", IpcTestRunPlatformCodec.UnityEditMode);
        Assert.Equal("PlayMode", IpcTestRunPlatformCodec.UnityPlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunPlatform_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)IpcTestRunPlatform.EditMode);
        Assert.Equal(1, (int)IpcTestRunPlatform.PlayMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunPlatformCodec_ToValue_ReturnsExpectedLiterals ()
    {
        Assert.Equal("editmode", IpcTestRunPlatformCodec.ToValue(IpcTestRunPlatform.EditMode));
        Assert.Equal("playmode", IpcTestRunPlatformCodec.ToValue(IpcTestRunPlatform.PlayMode));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTestRunPlatformCodec_ToUnityValue_ReturnsExpectedLiterals ()
    {
        Assert.Equal("EditMode", IpcTestRunPlatformCodec.ToUnityValue(IpcTestRunPlatform.EditMode));
        Assert.Equal("PlayMode", IpcTestRunPlatformCodec.ToUnityValue(IpcTestRunPlatform.PlayMode));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("editmode", true, IpcTestRunPlatform.EditMode)]
    [InlineData(" PLAYMODE ", true, IpcTestRunPlatform.PlayMode)]
    [InlineData("unsupported", false, null)]
    [InlineData("", false, null)]
    [InlineData(" ", false, null)]
    [InlineData(null, false, null)]
    public void IpcTestRunPlatformCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        IpcTestRunPlatform? expectedValue)
    {
        var result = IpcTestRunPlatformCodec.TryParse(value, out var testPlatform);

        Assert.Equal(expectedResult, result);
        if (expectedValue.HasValue)
        {
            Assert.Equal(expectedValue.Value, testPlatform);
        }
    }
}