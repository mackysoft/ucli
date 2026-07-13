using System.Text.Json;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class GuiSupervisorManifestJsonContractSerializerTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeThenDeserialize_WithProjectFingerprint_RoundTripsCanonicalValue ()
    {
        var source = new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-gui-supervisor-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero));

        var json = GuiSupervisorManifestJsonContractSerializer.Serialize(source);
        var roundTrip = GuiSupervisorManifestJsonContractSerializer.Deserialize(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(new ProjectFingerprint(ProjectFingerprintText), roundTrip.ProjectFingerprint);
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
}
