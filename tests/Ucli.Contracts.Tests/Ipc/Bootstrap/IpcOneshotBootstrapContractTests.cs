using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcOneshotBootstrapContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ArgumentsConstructor_WhenBootstrapIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new IpcOneshotBootstrapArguments(Guid.Empty));

        Assert.Equal("BootstrapId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnvelopeConstructor_WhenBootstrapIdIsEmpty_ThrowsArgumentException ()
    {
        var nowUtc = DateTimeOffset.UtcNow;

        var exception = Assert.Throws<ArgumentException>(() => CreateEnvelope(
            Guid.Empty,
            nowUtc,
            nowUtc.AddMinutes(1)));

        Assert.Equal("BootstrapId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnvelopeConstructor_WhenDeadlineDoesNotFollowCreation_ThrowsArgumentOutOfRangeException ()
    {
        var nowUtc = DateTimeOffset.UtcNow;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateEnvelope(
            Guid.NewGuid(),
            nowUtc,
            nowUtc));

        Assert.Equal("ExitDeadlineUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnvelopeToString_DoesNotExposeSessionToken ()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));

        Assert.Equal(nameof(IpcOneshotBootstrapEnvelope), envelope.ToString());
        Assert.DoesNotContain(envelope.SessionToken.GetEncodedValue(), envelope.ToString(), StringComparison.Ordinal);
    }

    private static IpcOneshotBootstrapEnvelope CreateEnvelope (
        Guid bootstrapId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset exitDeadlineUtc)
    {
        return new IpcOneshotBootstrapEnvelope(
            BootstrapId: bootstrapId,
            ParentProcessId: 1234,
            ParentProcessStartedAtUtc: createdAtUtc.AddMinutes(-1),
            ProjectFingerprint: new ProjectFingerprint(
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
            SessionToken: IpcSessionToken.CreateRandom(),
            CreatedAtUtc: createdAtUtc,
            ExitDeadlineUtc: exitDeadlineUtc,
            Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-oneshot"));
    }
}
