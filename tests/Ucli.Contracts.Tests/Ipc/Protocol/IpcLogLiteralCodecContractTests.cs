using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcLogLiteralCodecContractTests
{
    public static TheoryData<string, string, string> StableLiterals => new()
    {
        { "IpcDaemonLogsLevelCodec.All", IpcDaemonLogsLevelCodec.All, "all" },
        { "IpcDaemonLogsLevelCodec.Error", IpcDaemonLogsLevelCodec.Error, "error" },
        { "IpcDaemonLogsLevelCodec.Warning", IpcDaemonLogsLevelCodec.Warning, "warning" },
        { "IpcDaemonLogsLevelCodec.Info", IpcDaemonLogsLevelCodec.Info, "info" },
        { "IpcDaemonLogsQueryTargetCodec.Message", IpcDaemonLogsQueryTargetCodec.Message, "message" },
        { "IpcDaemonLogsQueryTargetCodec.Stack", IpcDaemonLogsQueryTargetCodec.Stack, "stack" },
        { "IpcDaemonLogsQueryTargetCodec.Both", IpcDaemonLogsQueryTargetCodec.Both, "both" },
        { "IpcUnityLogsSourceCodec.Compile", IpcUnityLogsSourceCodec.Compile, "compile" },
        { "IpcUnityLogsSourceCodec.Runtime", IpcUnityLogsSourceCodec.Runtime, "runtime" },
        { "IpcUnityLogsSourceCodec.All", IpcUnityLogsSourceCodec.All, "all" },
        { "IpcUnityLogsStackTraceModeCodec.None", IpcUnityLogsStackTraceModeCodec.None, "none" },
        { "IpcUnityLogsStackTraceModeCodec.Error", IpcUnityLogsStackTraceModeCodec.Error, "error" },
        { "IpcUnityLogsStackTraceModeCodec.All", IpcUnityLogsStackTraceModeCodec.All, "all" },
        { "IpcDaemonLogsCategoryCodec.All", IpcDaemonLogsCategoryCodec.All, "all" },
    };

    public static TheoryData<string?, bool, string?> LevelParseCases => new()
    {
        { "all", true, IpcDaemonLogsLevelCodec.All },
        { " WARNING ", true, IpcDaemonLogsLevelCodec.Warning },
        { "unsupported", false, null },
        { "", false, null },
        { " ", false, null },
        { null, false, null },
    };

    public static TheoryData<string?, bool, string?> QueryTargetParseCases => new()
    {
        { "message", true, IpcDaemonLogsQueryTargetCodec.Message },
        { " STACK ", true, IpcDaemonLogsQueryTargetCodec.Stack },
        { "both", true, IpcDaemonLogsQueryTargetCodec.Both },
        { "unsupported", false, null },
        { "", false, null },
        { " ", false, null },
        { null, false, null },
    };

    public static TheoryData<string?, bool, string?> DaemonQueryTargetParseCases => new()
    {
        { null, true, IpcDaemonLogsQueryTargetCodec.Message },
        { "", true, IpcDaemonLogsQueryTargetCodec.Message },
        { " message ", true, IpcDaemonLogsQueryTargetCodec.Message },
        { "both", true, IpcDaemonLogsQueryTargetCodec.Both },
        { "stack", false, null },
        { "unsupported", false, null },
    };

    public static TheoryData<string?, bool, string?> UnityQueryTargetParseCases => new()
    {
        { null, true, IpcDaemonLogsQueryTargetCodec.Message },
        { "", true, IpcDaemonLogsQueryTargetCodec.Message },
        { " stack ", true, IpcDaemonLogsQueryTargetCodec.Stack },
        { "both", true, IpcDaemonLogsQueryTargetCodec.Both },
        { "unsupported", false, null },
    };

    public static TheoryData<string?, bool, string?> UnitySourceParseCases => new()
    {
        { "compile", true, IpcUnityLogsSourceCodec.Compile },
        { " RUNTIME ", true, IpcUnityLogsSourceCodec.Runtime },
        { "all", true, IpcUnityLogsSourceCodec.All },
        { "unsupported", false, null },
        { "", false, null },
        { " ", false, null },
        { null, false, null },
    };

    public static TheoryData<string?, bool> UnitySourceAllCases => new()
    {
        { "all", true },
        { " ALL ", true },
        { "runtime", false },
        { "", false },
        { " ", false },
        { null, false },
    };

    public static TheoryData<string?, bool, string?> UnityStackTraceModeParseCases => new()
    {
        { "none", true, IpcUnityLogsStackTraceModeCodec.None },
        { " ERROR ", true, IpcUnityLogsStackTraceModeCodec.Error },
        { "all", true, IpcUnityLogsStackTraceModeCodec.All },
        { "unsupported", false, null },
        { "", false, null },
        { " ", false, null },
        { null, false, null },
    };

    public static TheoryData<string?, bool> DaemonCategoryAllCases => new()
    {
        { "all", true },
        { " ALL ", true },
        { "ipc", false },
        { "", false },
        { " ", false },
        { null, false },
    };

    [Theory]
    [MemberData(nameof(StableLiterals))]
    [Trait("Size", "Small")]
    public void LogCodecs_ExposeStableStringValues (
        string literalName,
        string value,
        string expectedValue)
    {
        Assert.False(string.IsNullOrWhiteSpace(literalName));

        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [MemberData(nameof(LevelParseCases))]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsLevelCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcDaemonLogsLevelCodec.TryParse(value, out var level);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, level);
    }

    [Theory]
    [MemberData(nameof(QueryTargetParseCases))]
    [Trait("Size", "Small")]
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
    [MemberData(nameof(DaemonQueryTargetParseCases))]
    [Trait("Size", "Small")]
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
    [MemberData(nameof(UnityQueryTargetParseCases))]
    [Trait("Size", "Small")]
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

    [Theory]
    [MemberData(nameof(UnitySourceParseCases))]
    [Trait("Size", "Small")]
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
    [MemberData(nameof(UnitySourceAllCases))]
    [Trait("Size", "Small")]
    public void IpcUnityLogsSourceCodec_IsAll_ReturnsExpectedResult (
        string? value,
        bool expectedResult)
    {
        var result = IpcUnityLogsSourceCodec.IsAll(value);

        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [MemberData(nameof(UnityStackTraceModeParseCases))]
    [Trait("Size", "Small")]
    public void IpcUnityLogsStackTraceModeCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        string? expectedValue)
    {
        var result = IpcUnityLogsStackTraceModeCodec.TryParse(value, out var stackTraceMode);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedValue, stackTraceMode);
    }

    [Theory]
    [MemberData(nameof(DaemonCategoryAllCases))]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsCategoryCodec_IsAll_ReturnsExpectedResult (
        string? value,
        bool expectedResult)
    {
        var result = IpcDaemonLogsCategoryCodec.IsAll(value);

        Assert.Equal(expectedResult, result);
    }
}
