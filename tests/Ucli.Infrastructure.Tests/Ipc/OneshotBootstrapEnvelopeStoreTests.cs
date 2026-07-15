using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc;

public sealed class OneshotBootstrapEnvelopeStoreTests
{
    private static readonly ProjectFingerprint ProjectFingerprint = new(
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_ThenRead_ReturnsCompleteImmutableEnvelope ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "roundtrip");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));

        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);

        var actual = OneshotBootstrapEnvelopeStore.Read(
            scope.FullPath,
            ProjectFingerprint,
            envelope.BootstrapId,
            nowUtc);
        Assert.Equal(envelope, actual);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_OnPosix_PublishesOwnerOnlyEnvelope ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "owner-only");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));

        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);

        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            ProjectFingerprint,
            envelope.BootstrapId);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(path));
    }

    [Theory]
    [InlineData(MalformedEnvelopeContent.InvalidJson)]
    [InlineData(MalformedEnvelopeContent.InvalidUtf8)]
    [Trait("Size", "Medium")]
    public void Create_WhenMalformedMaintenanceCandidateExists_CreatesIndependentGeneration (
        MalformedEnvelopeContent malformedContent)
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "malformed-maintenance");
        var nowUtc = DateTimeOffset.UtcNow;
        var seedEnvelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, seedEnvelope);
        var malformedPath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            ProjectFingerprint,
            Guid.NewGuid());
        WriteMalformedEnvelope(malformedPath, malformedContent);
        var expected = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));

        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, expected);

        Assert.Equal(
            expected,
            OneshotBootstrapEnvelopeStore.Read(
                scope.FullPath,
                ProjectFingerprint,
                expected.BootstrapId,
                nowUtc));
        Assert.True(File.Exists(malformedPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Read_WhenEnvelopeHasExpired_ThrowsInvalidDataException ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "expired");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(
            scope.FullPath,
            Guid.NewGuid(),
            nowUtc.AddMinutes(-2),
            nowUtc.AddMinutes(-1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);

        var exception = Assert.Throws<InvalidDataException>(() => OneshotBootstrapEnvelopeStore.Read(
            scope.FullPath,
            ProjectFingerprint,
            envelope.BootstrapId,
            nowUtc));

        Assert.Contains("expired", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Read_WhenStoredIdentifierWasTampered_ThrowsInvalidDataException ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "tampered-id");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            ProjectFingerprint,
            envelope.BootstrapId);
        var tamperedId = Guid.NewGuid();
        File.WriteAllText(
            path,
            File.ReadAllText(path).Replace(
                envelope.BootstrapId.ToString("D"),
                tamperedId.ToString("D"),
                StringComparison.Ordinal));

        var exception = Assert.Throws<InvalidDataException>(() => OneshotBootstrapEnvelopeStore.Read(
            scope.FullPath,
            ProjectFingerprint,
            envelope.BootstrapId,
            nowUtc));

        Assert.Contains("identifier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Read_WhenDifferentIdentifierIsRequested_ThrowsFileNotFoundException ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "different-id");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);

        Assert.Throws<FileNotFoundException>(() => OneshotBootstrapEnvelopeStore.Read(
            scope.FullPath,
            ProjectFingerprint,
            Guid.NewGuid(),
            nowUtc));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void TryDeleteIfOwned_WhenGenerationDiffers_PreservesCurrentEnvelope ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "ownership");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);
        var differentGeneration = CreateEnvelope(
            scope.FullPath,
            envelope.BootstrapId,
            nowUtc,
            nowUtc.AddMinutes(2));

        Assert.False(OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(scope.FullPath, differentGeneration));
        Assert.True(OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(scope.FullPath, envelope));
    }

    [Theory]
    [InlineData(MalformedEnvelopeContent.InvalidJson)]
    [InlineData(MalformedEnvelopeContent.InvalidUtf8)]
    [Trait("Size", "Medium")]
    public void TryDeleteIfOwned_WhenStoredEnvelopeIsMalformed_ReturnsFalseAndPreservesFile (
        MalformedEnvelopeContent malformedContent)
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "malformed-ownership");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            ProjectFingerprint,
            envelope.BootstrapId);
        WriteMalformedEnvelope(path, malformedContent);

        var deleted = OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(scope.FullPath, envelope);

        Assert.False(deleted);
        Assert.True(File.Exists(path));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenIdentifierAlreadyExists_DoesNotReplaceGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "duplicate");
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);

        Assert.Throws<IOException>(() => OneshotBootstrapEnvelopeStore.Create(
            scope.FullPath,
            CreateEnvelope(scope.FullPath, envelope.BootstrapId, nowUtc, nowUtc.AddMinutes(2))));
        Assert.Equal(
            envelope,
            OneshotBootstrapEnvelopeStore.Read(
                scope.FullPath,
                ProjectFingerprint,
                envelope.BootstrapId,
                nowUtc));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenExpiredEnvelopeUsesForeignFileName_PreservesForeignFile ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "foreign-maintenance-name");
        var nowUtc = DateTimeOffset.UtcNow;
        var expiredEnvelope = CreateEnvelope(
            scope.FullPath,
            Guid.NewGuid(),
            nowUtc.AddMinutes(-2),
            nowUtc.AddMinutes(-1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, expiredEnvelope);
        var ownedPath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            ProjectFingerprint,
            expiredEnvelope.BootstrapId);
        var foreignPath = Path.Combine(Path.GetDirectoryName(ownedPath)!, "foreign.json");
        File.Move(ownedPath, foreignPath);

        OneshotBootstrapEnvelopeStore.Create(
            scope.FullPath,
            CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1)));

        Assert.True(File.Exists(foreignPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenMaintenanceBudgetIsConsumedByForeignFiles_DoesNotScanPastBudget ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "bounded-maintenance");
        var nowUtc = DateTimeOffset.UtcNow;
        var directoryPath = UcliStoragePathResolver.ResolveOneshotBootstrapDirectory(
            scope.FullPath,
            ProjectFingerprint);
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        WriteForeignMaintenanceFiles(directoryPath);

        var expiredBootstrapId = Guid.Empty;
        var expiredPath = string.Empty;
        for (var attempt = 1; attempt <= 4096; attempt++)
        {
            var candidateBootstrapId = Guid.Parse($"00000000-0000-0000-0000-{attempt:x12}");
            var candidatePath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
                scope.FullPath,
                ProjectFingerprint,
                candidateBootstrapId);
            File.WriteAllText(candidatePath, "{}");
            var candidateIndex = Array.IndexOf(
                Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly).ToArray(),
                candidatePath);
            if (candidateIndex >= 128)
            {
                expiredBootstrapId = candidateBootstrapId;
                expiredPath = candidatePath;
                break;
            }

            File.Delete(candidatePath);
        }

        Assert.NotEqual(Guid.Empty, expiredBootstrapId);
        using (var envelopeScope = TestDirectories.CreateTempScope(
                   "oneshot-bootstrap-envelope",
                   "bounded-maintenance-envelope"))
        {
            var expiredEnvelope = CreateEnvelope(
                envelopeScope.FullPath,
                expiredBootstrapId,
                nowUtc.AddMinutes(-2),
                nowUtc.AddMinutes(-1));
            OneshotBootstrapEnvelopeStore.Create(envelopeScope.FullPath, expiredEnvelope);
            File.WriteAllBytes(
                expiredPath,
                File.ReadAllBytes(UcliStoragePathResolver.ResolveOneshotBootstrapPath(
                    envelopeScope.FullPath,
                    ProjectFingerprint,
                    expiredBootstrapId)));
        }

        OneshotBootstrapEnvelopeStore.Create(
            scope.FullPath,
            CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1)));

        Assert.True(File.Exists(expiredPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenExpiredEnvelopeIdentifierDoesNotMatchFileName_PreservesUnprovenFile ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "mismatched-maintenance-id");
        var nowUtc = DateTimeOffset.UtcNow;
        var expiredEnvelope = CreateEnvelope(
            scope.FullPath,
            Guid.NewGuid(),
            nowUtc.AddMinutes(-2),
            nowUtc.AddMinutes(-1));
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, expiredEnvelope);
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            ProjectFingerprint,
            expiredEnvelope.BootstrapId);
        File.WriteAllText(
            path,
            File.ReadAllText(path).Replace(
                expiredEnvelope.BootstrapId.ToString("D"),
                Guid.NewGuid().ToString("D"),
                StringComparison.Ordinal));

        OneshotBootstrapEnvelopeStore.Create(
            scope.FullPath,
            CreateEnvelope(scope.FullPath, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1)));

        Assert.True(File.Exists(path));
    }

    private static IpcOneshotBootstrapEnvelope CreateEnvelope (
        string storageRoot,
        Guid bootstrapId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset exitDeadlineUtc)
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return new IpcOneshotBootstrapEnvelope(
            BootstrapId: bootstrapId,
            ParentProcessId: process.Id,
            ParentProcessStartedAtUtc: new DateTimeOffset(process.StartTime.ToUniversalTime()),
            ProjectFingerprint: ProjectFingerprint,
            SessionToken: IpcSessionToken.CreateRandom(),
            CreatedAtUtc: createdAtUtc,
            ExitDeadlineUtc: exitDeadlineUtc,
            Endpoint: UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, ProjectFingerprint));
    }

    private static void WriteForeignMaintenanceFiles (string directoryPath)
    {
        for (var index = 0; index < 128; index++)
        {
            File.WriteAllText(
                Path.Combine(directoryPath, $"000-invalid-{index:D3}.json"),
                "{}");
        }
    }

    private static void WriteMalformedEnvelope (
        string path,
        MalformedEnvelopeContent malformedContent)
    {
        switch (malformedContent)
        {
            case MalformedEnvelopeContent.InvalidJson:
                File.WriteAllText(path, "{");
                break;
            case MalformedEnvelopeContent.InvalidUtf8:
                File.WriteAllBytes(path, new byte[] { 0xC3, 0x28 });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(malformedContent), malformedContent, null);
        }
    }

    public enum MalformedEnvelopeContent
    {
        InvalidJson = 0,
        InvalidUtf8 = 1,
    }
}
