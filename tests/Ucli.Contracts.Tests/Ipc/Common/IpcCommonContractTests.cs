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
    public void IpcBatchmodeBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliBootstrapTarget", IpcBatchmodeBootstrapArgumentNames.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBatchmodeBootstrapTargetValues_HasStableStringValues ()
    {
        Assert.Equal("daemon", IpcBatchmodeBootstrapTargetValues.Daemon);
        Assert.Equal("oneshot", IpcBatchmodeBootstrapTargetValues.Oneshot);
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
    public void IpcOneshotBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliOneshotRequestPath", IpcOneshotBootstrapArgumentNames.RequestPath);
        Assert.Equal("-ucliOneshotResponsePath", IpcOneshotBootstrapArgumentNames.ResponsePath);
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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, true, IpcDaemonLogsQueryTargetCodec.Message)]
    [InlineData("", true, IpcDaemonLogsQueryTargetCodec.Message)]
    [InlineData(" message ", true, IpcDaemonLogsQueryTargetCodec.Message)]
    [InlineData("both", true, IpcDaemonLogsQueryTargetCodec.Both)]
    [InlineData("stack", false, null)]
    [InlineData("unsupported", false, null)]
    public void IpcDaemonLogsQueryTargetCodec_TryParseForDaemonLogs_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcDaemonLogsQueryTargetCodec.TryParseForDaemonLogs(value, out var queryTarget, out var errorMessage);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, string.IsNullOrEmpty(queryTarget) ? null : queryTarget);
        if (expectedResult)
        {
            Assert.Null(errorMessage);
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsQueryTargetCodec_CreateDaemonLogsUnsupportedValueMessage_ReturnsExpectedText ()
    {
        var message = IpcDaemonLogsQueryTargetCodec.CreateDaemonLogsUnsupportedValueMessage("unsupported");

        Assert.Equal("queryTarget must be one of: message, both. Actual: unsupported.", message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsQueryTargetCodec_CreateDaemonLogsStackNotSupportedMessage_ReturnsExpectedText ()
    {
        var message = IpcDaemonLogsQueryTargetCodec.CreateDaemonLogsStackNotSupportedMessage();

        Assert.Equal("queryTarget 'stack' is not supported for daemon logs. Supported: message, both.", message);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, true, IpcDaemonLogsQueryTargetCodec.Message)]
    [InlineData("", true, IpcDaemonLogsQueryTargetCodec.Message)]
    [InlineData(" stack ", true, IpcDaemonLogsQueryTargetCodec.Stack)]
    [InlineData("both", true, IpcDaemonLogsQueryTargetCodec.Both)]
    [InlineData("unsupported", false, null)]
    public void IpcDaemonLogsQueryTargetCodec_TryParseForUnityLogs_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcDaemonLogsQueryTargetCodec.TryParseForUnityLogs(value, out var queryTarget, out var errorMessage);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, string.IsNullOrEmpty(queryTarget) ? null : queryTarget);
        if (expectedResult)
        {
            Assert.Null(errorMessage);
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsQueryTargetCodec_CreateUnsupportedValueMessage_ReturnsExpectedText ()
    {
        var message = IpcDaemonLogsQueryTargetCodec.CreateUnsupportedValueMessage("unsupported");

        Assert.Equal("queryTarget must be one of: message, stack, both. Actual: unsupported.", message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsSourceCodec_HasStableStringValues ()
    {
        Assert.Equal("compile", IpcUnityLogsSourceCodec.Compile);
        Assert.Equal("runtime", IpcUnityLogsSourceCodec.Runtime);
        Assert.Equal("all", IpcUnityLogsSourceCodec.All);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("compile", true, IpcUnityLogsSourceCodec.Compile)]
    [InlineData(" RUNTIME ", true, IpcUnityLogsSourceCodec.Runtime)]
    [InlineData("all", true, IpcUnityLogsSourceCodec.All)]
    [InlineData("unsupported", false, null)]
    [InlineData("", false, null)]
    [InlineData(" ", false, null)]
    [InlineData(null, false, null)]
    public void IpcUnityLogsSourceCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcUnityLogsSourceCodec.TryParse(value, out var source);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, source);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("all", true)]
    [InlineData(" ALL ", true)]
    [InlineData("runtime", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(null, false)]
    public void IpcUnityLogsSourceCodec_IsAll_ReturnsExpectedResult (
        string? value,
        bool expectedResult)
    {
        var result = IpcUnityLogsSourceCodec.IsAll(value);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsStackTraceModeCodec_HasStableStringValues ()
    {
        Assert.Equal("none", IpcUnityLogsStackTraceModeCodec.None);
        Assert.Equal("error", IpcUnityLogsStackTraceModeCodec.Error);
        Assert.Equal("all", IpcUnityLogsStackTraceModeCodec.All);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("none", true, IpcUnityLogsStackTraceModeCodec.None)]
    [InlineData(" ERROR ", true, IpcUnityLogsStackTraceModeCodec.Error)]
    [InlineData("all", true, IpcUnityLogsStackTraceModeCodec.All)]
    [InlineData("unsupported", false, null)]
    [InlineData("", false, null)]
    [InlineData(" ", false, null)]
    [InlineData(null, false, null)]
    public void IpcUnityLogsStackTraceModeCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcUnityLogsStackTraceModeCodec.TryParse(value, out var stackTraceMode);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, stackTraceMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsReadRequestNormalizer_TryNormalize_WhenStackTraceModeIsNone_ClearsStackTraceLimits ()
    {
        var result = IpcUnityLogsReadRequestNormalizer.TryNormalize(
            new IpcUnityLogsReadRequest(
                Tail: 4,
                After: "stream-1:4",
                Since: "2026-03-05T10:35:22.0000000+09:00",
                Until: "2026-03-05T10:36:22.0000000+09:00",
                Level: null,
                Query: " socket ",
                QueryTarget: null,
                Source: null,
                StackTrace: "none",
                StackTraceMaxFrames: 8,
                StackTraceMaxChars: 4096),
            out var normalizedRequest,
            out var sinceTimestamp,
            out var untilTimestamp,
            out var errorMessage);

        Assert.True(result);
        Assert.NotNull(normalizedRequest);
        Assert.Equal(IpcDaemonLogsLevelCodec.All, normalizedRequest.Level);
        Assert.Equal(IpcDaemonLogsQueryTargetCodec.Message, normalizedRequest.QueryTarget);
        Assert.Equal(IpcUnityLogsSourceCodec.All, normalizedRequest.Source);
        Assert.Equal(IpcUnityLogsStackTraceModeCodec.None, normalizedRequest.StackTrace);
        Assert.Null(normalizedRequest.StackTraceMaxFrames);
        Assert.Null(normalizedRequest.StackTraceMaxChars);
        Assert.Equal("socket", normalizedRequest.Query);
        Assert.True(sinceTimestamp.HasValue);
        Assert.True(untilTimestamp.HasValue);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsReadRequestNormalizer_TryNormalize_WhenStackTraceCharLimitIsTooSmall_ReturnsError ()
    {
        var result = IpcUnityLogsReadRequestNormalizer.TryNormalize(
            new IpcUnityLogsReadRequest(
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: IpcUnityLogsStackTraceModeCodec.All,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars - 1),
            out var normalizedRequest,
            out _,
            out _,
            out var errorMessage);

        Assert.False(result);
        Assert.Null(normalizedRequest);
        Assert.Equal(
            $"stackTraceMaxChars must be between {IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars} and {IpcUnityLogsReadRequestNormalizer.MaximumStackTraceMaxChars}. Actual: {IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars - 1}.",
            errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsReadRequestNormalizer_TryNormalize_WhenRequestIsValid_ReturnsCanonicalValues ()
    {
        var result = IpcDaemonLogsReadRequestNormalizer.TryNormalize(
            new IpcDaemonLogsReadRequest(
                Tail: 4,
                After: "stream-1:4",
                Since: "2026-03-05T10:35:22.0000000+09:00",
                Until: "2026-03-05T10:36:22.0000000+09:00",
                Level: null,
                Query: " socket ",
                QueryTarget: null,
                Category: " ALL "),
            out var normalizedRequest,
            out var sinceTimestamp,
            out var untilTimestamp,
            out var errorMessage);

        Assert.True(result);
        Assert.NotNull(normalizedRequest);
        Assert.Equal(IpcDaemonLogsLevelCodec.All, normalizedRequest.Level);
        Assert.Equal(IpcDaemonLogsQueryTargetCodec.Message, normalizedRequest.QueryTarget);
        Assert.Equal(IpcDaemonLogsCategoryCodec.All, normalizedRequest.Category);
        Assert.Equal("socket", normalizedRequest.Query);
        Assert.True(sinceTimestamp.HasValue);
        Assert.True(untilTimestamp.HasValue);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsReadRequestNormalizer_TryNormalize_WhenQueryTargetIsStack_ReturnsError ()
    {
        var result = IpcDaemonLogsReadRequestNormalizer.TryNormalize(
            new IpcDaemonLogsReadRequest(
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: IpcDaemonLogsQueryTargetCodec.Stack,
                Category: null),
            out var normalizedRequest,
            out _,
            out _,
            out var errorMessage);

        Assert.False(result);
        Assert.Null(normalizedRequest);
        Assert.Equal(IpcDaemonLogsQueryTargetCodec.CreateDaemonLogsStackNotSupportedMessage(), errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsCategoryCodec_HasStableStringValues ()
    {
        Assert.Equal("all", IpcDaemonLogsCategoryCodec.All);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("all", true)]
    [InlineData(" ALL ", true)]
    [InlineData("ipc", false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(null, false)]
    public void IpcDaemonLogsCategoryCodec_IsAll_ReturnsExpectedResult (
        string? value,
        bool expectedResult)
    {
        var result = IpcDaemonLogsCategoryCodec.IsAll(value);

        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("2026-03-05T10:35:22.0000000+09:00", true, true)]
    [InlineData("2026-03-05T01:35:22.0000000Z", true, true)]
    [InlineData("2026-03-05", false, false)]
    [InlineData("2026-03-05+09:00", false, false)]
    [InlineData("2026-03-05T10:35:22", false, false)]
    [InlineData("invalid", false, false)]
    [InlineData("", true, false)]
    [InlineData(" ", true, false)]
    [InlineData(null, true, false)]
    public void IpcIso8601TimestampCodec_TryParseOptionalWithTimezoneOffset_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        bool expectedHasValue)
    {
        var result = IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(value, out var timestamp);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedHasValue, timestamp.HasValue);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcLogCursorCodec_EncodeAndTryParse_RoundTripsValues ()
    {
        var cursor = IpcLogCursorCodec.Encode("stream-1", 42);

        var result = IpcLogCursorCodec.TryParse(cursor, out var streamId, out var sequence);

        Assert.True(result);
        Assert.Equal("stream-1", streamId);
        Assert.Equal(42, sequence);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("stream")]
    [InlineData("stream-1:")]
    [InlineData(":1")]
    [InlineData("stream-1:-1")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void IpcLogCursorCodec_TryParse_InvalidValue_ReturnsFalse (string? value)
    {
        var result = IpcLogCursorCodec.TryParse(value, out var streamId, out var sequence);

        Assert.False(result);
        Assert.Equal(string.Empty, streamId);
        Assert.Equal(default, sequence);
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