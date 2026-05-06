using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class JsonStringContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_ReturnsMissingError_WhenRequiredPropertyIsAbsent ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = 1,
        });

        var result = JsonStringContractReader.TryRead(
            jsonObject,
            "requestId",
            JsonStringPresenceRequirement.Required,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out var value,
            out var error);

        Assert.False(result);
        Assert.Null(value);
        JsonStringReadErrorAssert.Equal(error, JsonStringReadErrorKind.Missing, "requestId");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_ReturnsNullWithoutError_WhenOptionalLoosePropertyIsNonString ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            requestId = 123,
        });

        var result = JsonStringContractReader.TryRead(
            jsonObject,
            "requestId",
            JsonStringPresenceRequirement.OptionalLoose,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out var value,
            out var error);

        Assert.True(result);
        Assert.Null(value);
        JsonStringReadErrorAssert.Equal(error, JsonStringReadErrorKind.None, string.Empty);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_ReturnsTypeMismatch_WhenOptionalStrictPropertyIsNonString ()
    {
        using var parsed = JsonDocument.Parse("""{"as":10}""");

        var result = JsonStringContractReader.TryRead(
            parsed.RootElement,
            "as",
            JsonStringPresenceRequirement.OptionalStrict,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out _,
            out var error);

        Assert.False(result);
        JsonStringReadErrorAssert.Equal(error, JsonStringReadErrorKind.TypeMismatch, "as");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_ReturnsOuterWhitespaceError_WhenOuterWhitespaceIsRejected ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            op = " ucli.scene.open ",
        });

        var result = JsonStringContractReader.TryRead(
            jsonObject,
            "op",
            JsonStringPresenceRequirement.Required,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out _,
            out var error);

        Assert.False(result);
        JsonStringReadErrorAssert.Equal(error, JsonStringReadErrorKind.OuterWhitespace, "op");
    }
}
