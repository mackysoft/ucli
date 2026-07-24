namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorInstanceManifestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenIssuedAtUtcHasNonZeroOffset_RejectsValue ()
    {
        var issuedAtUtc = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(9));

        var exception = Assert.Throws<ArgumentException>(
            () => new SupervisorInstanceManifest(
                processId: 2468,
                sessionToken: IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
                endpoint: SupervisorTransportEndpoint.FromNamedPipeAddress("ucli-supervisor-test"),
                issuedAtUtc: issuedAtUtc));

        Assert.Equal("issuedAtUtc", exception.ParamName);
    }
}
