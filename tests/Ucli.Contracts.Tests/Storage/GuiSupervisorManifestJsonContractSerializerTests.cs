using System.Globalization;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class GuiSupervisorManifestJsonContractSerializerTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string CanonicalSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeThenDeserialize_WithTypedValues_PreservesFlatSchemaAndRoundTrips ()
    {
        var source = new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: IpcSessionToken.CreateRandom(),
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-supervisor-endpoint"),
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero));

        var json = GuiSupervisorManifestJsonContractSerializer.Serialize(source);
        var roundTrip = GuiSupervisorManifestJsonContractSerializer.Deserialize(json);
        using var document = JsonDocument.Parse(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(source.SessionToken, roundTrip.SessionToken);
        Assert.Equal(new ProjectFingerprint(ProjectFingerprintText), roundTrip.ProjectFingerprint);
        Assert.Equal(source.Endpoint, roundTrip.Endpoint);
        Assert.Equal(
            source.SessionToken.GetEncodedValue(),
            document.RootElement.GetProperty("sessionToken").GetString());
        Assert.Equal("namedPipe", document.RootElement.GetProperty("endpointTransportKind").GetString());
        Assert.Equal(source.Endpoint.Address, document.RootElement.GetProperty("endpointAddress").GetString());
        Assert.False(document.RootElement.TryGetProperty("endpoint", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WhenProjectFingerprintIsInvalid_ThrowsJsonException ()
    {
        const string Json = """
            {
              "schemaVersion": 1,
              "sessionToken": "session-token",
              "projectFingerprint": "not-a-project-fingerprint",
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-gui-supervisor-endpoint",
              "processId": 1234,
              "issuedAtUtc": "2026-07-13T00:00:00+00:00"
            }
            """;

        var exception = Assert.Throws<JsonException>(() =>
            GuiSupervisorManifestJsonContractSerializer.Deserialize(Json));

        Assert.Contains("project fingerprint is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("session-token", "namedPipe", "ucli-gui-supervisor-endpoint")]
    [InlineData(CanonicalSessionToken, "invalid-transport", "ucli-gui-supervisor-endpoint")]
    [InlineData(CanonicalSessionToken, "namedPipe", "/tmp/ucli-gui-supervisor.sock")]
    [Trait("Size", "Small")]
    public void Deserialize_WhenSessionTokenOrEndpointIsInvalid_ThrowsJsonException (
        string sessionToken,
        string endpointTransportKind,
        string endpointAddress)
    {
        var json = $$"""
            {
              "schemaVersion": 1,
              "sessionToken": "{{sessionToken}}",
              "projectFingerprint": "{{ProjectFingerprintText}}",
              "endpointTransportKind": "{{endpointTransportKind}}",
              "endpointAddress": "{{endpointAddress}}",
              "processId": 1234,
              "issuedAtUtc": "2026-07-13T00:00:00+00:00"
            }
            """;

        Assert.Throws<JsonException>(() => GuiSupervisorManifestJsonContractSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData(nameof(GuiSupervisorManifestJsonContract.ProcessStartedAtUtc), "0001-01-01T00:00:00+00:00")]
    [InlineData(nameof(GuiSupervisorManifestJsonContract.ProcessStartedAtUtc), "2026-07-13T09:00:01+09:00")]
    [InlineData(nameof(GuiSupervisorManifestJsonContract.IssuedAtUtc), "0001-01-01T00:00:00+00:00")]
    [InlineData(nameof(GuiSupervisorManifestJsonContract.IssuedAtUtc), "2026-07-13T09:00:00+09:00")]
    [Trait("Size", "Small")]
    public void Constructor_WhenTimestampIsInvalid_ThrowsArgumentException (
        string propertyName,
        string timestampText)
    {
        var invalidTimestamp = DateTimeOffset.Parse(timestampText, CultureInfo.InvariantCulture);
        var validTimestamp = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<ArgumentException>(() => new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: IpcSessionToken.CreateRandom(),
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-supervisor-endpoint"),
            ProcessId: 1234,
            ProcessStartedAtUtc: propertyName == nameof(GuiSupervisorManifestJsonContract.ProcessStartedAtUtc)
                ? invalidTimestamp
                : validTimestamp,
            IssuedAtUtc: propertyName == nameof(GuiSupervisorManifestJsonContract.IssuedAtUtc)
                ? invalidTimestamp
                : validTimestamp));

        Assert.Equal(propertyName, exception.ParamName);
    }

    [Theory]
    [InlineData(nameof(GuiSupervisorManifestJsonContract.SessionToken))]
    [InlineData(nameof(GuiSupervisorManifestJsonContract.Endpoint))]
    [Trait("Size", "Small")]
    public void Constructor_WhenTypedValueIsNull_ThrowsArgumentNullException (string propertyName)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: propertyName == nameof(GuiSupervisorManifestJsonContract.SessionToken)
                ? null!
                : IpcSessionToken.CreateRandom(),
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            Endpoint: propertyName == nameof(GuiSupervisorManifestJsonContract.Endpoint)
                ? null!
                : new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-supervisor-endpoint"),
            ProcessId: 1234,
            ProcessStartedAtUtc: null,
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(propertyName, exception.ParamName);
    }

    [Theory]
    [InlineData("0001-01-01T00:00:00+00:00", "2026-07-13T00:00:00+00:00")]
    [InlineData("2026-07-13T09:00:01+09:00", "2026-07-13T00:00:00+00:00")]
    [InlineData("2026-07-13T00:00:01+00:00", "0001-01-01T00:00:00+00:00")]
    [InlineData("2026-07-13T00:00:01+00:00", "2026-07-13T09:00:00+09:00")]
    [Trait("Size", "Small")]
    public void Deserialize_WhenTimestampIsInvalid_ThrowsJsonException (
        string processStartedAtUtc,
        string issuedAtUtc)
    {
        var json = $$"""
            {
              "schemaVersion": 1,
              "sessionToken": "{{CanonicalSessionToken}}",
              "projectFingerprint": "{{ProjectFingerprintText}}",
              "endpointTransportKind": "namedPipe",
              "endpointAddress": "ucli-gui-supervisor-endpoint",
              "processId": 1234,
              "processStartedAtUtc": "{{processStartedAtUtc}}",
              "issuedAtUtc": "{{issuedAtUtc}}"
            }
            """;

        Assert.Throws<JsonException>(() => GuiSupervisorManifestJsonContractSerializer.Deserialize(json));
    }
}
