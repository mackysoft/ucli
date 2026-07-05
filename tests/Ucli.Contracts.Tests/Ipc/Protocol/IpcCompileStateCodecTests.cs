using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcCompileStateCodecTests
{
    private static readonly CompileStateParseCase[] CompileStateParseCases =
    [
        new("ready", ExpectedResult: true, IpcCompileStateCodec.Ready),
        new(" compiling ", ExpectedResult: true, IpcCompileStateCodec.Compiling),
        new("READY", ExpectedResult: false, ExpectedValue: null),
        new("unsupported", ExpectedResult: false, ExpectedValue: null),
        new("", ExpectedResult: false, ExpectedValue: null),
        new(" ", ExpectedResult: false, ExpectedValue: null),
        new(null, ExpectedResult: false, ExpectedValue: null),
    ];

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

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileStateCodec_TryParse_ReturnsExpectedResult ()
    {
        foreach (var testCase in CompileStateParseCases)
        {
            var result = IpcCompileStateCodec.TryParse(testCase.Value, out var compileState);

            Assert.Equal(testCase.ExpectedResult, result);
            Assert.Equal(testCase.ExpectedValue, compileState);
        }
    }

    private sealed record CompileStateParseCase (
        string? Value,
        bool ExpectedResult,
        string? ExpectedValue);
}
