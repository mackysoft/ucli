using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Persistence;

public sealed class ProgramRunFixedContextTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AuthorizationSnapshot_RejectsAnInvalidDigest ()
    {
        Assert.Throws<FormatException>(() => new ProgramEffectiveAuthorizationSnapshot(
                AllowDangerous: true,
                AllowPlayMode: false,
                Digest: "not-a-digest",
                CapturedAtUtc: new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero))
            .Validate());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AuthorizationSnapshot_RejectsDigestThatDoesNotMatchEffectivePermissions ()
    {
        var snapshot = new ProgramEffectiveAuthorizationSnapshot(
            AllowDangerous: false,
            AllowPlayMode: false,
            Digest: IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(true, false).ToString(),
            CapturedAtUtc: new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero));

        Assert.Throws<ArgumentException>(snapshot.Validate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConfigurationSnapshot_RejectsDigestThatDoesNotMatchEffectiveSettings ()
    {
        var settings = new ProgramEffectiveConfigurationSnapshot(1, OperationPolicy.Safe, PlanTokenMode.Optional,
            ReadIndexMode.RequireFresh, [], 1000, new Dictionary<string, int>(),
            Sha256Digest.Parse(new string('d', 64)), new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero));

        Assert.Throws<ArgumentException>(settings.Validate);
    }
}
