using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Authorization;

public sealed class SessionTokenContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryReadSessionToken_WithValidObject_ReturnsToken ()
    {
        using var document = JsonDocument.Parse("""{"sessionToken":"token-123"}""");

        var result = SessionTokenContractReader.TryReadSessionToken(
            document.RootElement,
            out var token,
            out var error);

        Assert.True(result);
        Assert.Equal("token-123", token);
        Assert.False(error.IsRootTypeMismatch);
        Assert.Equal(JsonStringReadErrorKind.None, error.JsonStringReadErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadSessionToken_WhenRootIsNotObject_ReturnsRootTypeMismatch ()
    {
        using var document = JsonDocument.Parse("[]");

        var result = SessionTokenContractReader.TryReadSessionToken(
            document.RootElement,
            out var token,
            out var error);

        Assert.False(result);
        Assert.Equal(string.Empty, token);
        Assert.True(error.IsRootTypeMismatch);
        Assert.Equal(JsonStringReadErrorKind.None, error.JsonStringReadErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadSessionToken_WhenPropertyIsMissing_ReturnsMissing ()
    {
        using var document = JsonDocument.Parse("""{"schemaVersion":1}""");

        var result = SessionTokenContractReader.TryReadSessionToken(
            document.RootElement,
            out var token,
            out var error);

        Assert.False(result);
        Assert.Equal(string.Empty, token);
        Assert.False(error.IsRootTypeMismatch);
        Assert.Equal(JsonStringReadErrorKind.Missing, error.JsonStringReadErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadSessionToken_WhenPropertyTypeIsInvalid_ReturnsTypeMismatch ()
    {
        using var document = JsonDocument.Parse("""{"sessionToken":123}""");

        var result = SessionTokenContractReader.TryReadSessionToken(
            document.RootElement,
            out var token,
            out var error);

        Assert.False(result);
        Assert.Equal(string.Empty, token);
        Assert.False(error.IsRootTypeMismatch);
        Assert.Equal(JsonStringReadErrorKind.TypeMismatch, error.JsonStringReadErrorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadSessionToken_WhenTokenIsWhitespace_ReturnsEmptyOrWhitespace ()
    {
        using var document = JsonDocument.Parse("""{"sessionToken":"   "}""");

        var result = SessionTokenContractReader.TryReadSessionToken(
            document.RootElement,
            out var token,
            out var error);

        Assert.False(result);
        Assert.Equal(string.Empty, token);
        Assert.False(error.IsRootTypeMismatch);
        Assert.Equal(JsonStringReadErrorKind.EmptyOrWhitespace, error.JsonStringReadErrorKind);
    }
}
