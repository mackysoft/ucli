namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorInstanceManifestJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TextRepresentations_DoNotExposeSessionToken ()
    {
        const string SessionToken = "sensitive-supervisor-instance-token-DO-NOT-LOG";
        var contract = new SupervisorInstanceManifestJsonContract(
            ProcessId: 1234,
            SessionToken: SessionToken,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-endpoint",
            IssuedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero));

        string[] textRepresentations =
        [
            contract.ToString(),
            $"Contract={contract}",
            new DiagnosticEnvelope(contract).ToString(),
        ];

        Assert.All(
            textRepresentations,
            text => Assert.DoesNotContain(SessionToken, text, StringComparison.Ordinal));
    }

    private sealed record DiagnosticEnvelope (object Value);
}
