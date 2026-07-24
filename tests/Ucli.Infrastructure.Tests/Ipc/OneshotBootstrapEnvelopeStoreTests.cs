using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Execution;
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
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));

        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);

        var actual = OneshotBootstrapEnvelopeStore.Read(
            storageRoot,
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
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));

        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);

        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            ProjectFingerprint,
            envelope.BootstrapId);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(path.Value));
    }

    [Theory]
    [InlineData(MalformedEnvelopeContent.InvalidJson)]
    [InlineData(MalformedEnvelopeContent.InvalidUtf8)]
    [Trait("Size", "Medium")]
    public void Create_WhenMalformedMaintenanceCandidateExists_CreatesIndependentGeneration (
        MalformedEnvelopeContent malformedContent)
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "malformed-maintenance");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var seedEnvelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, seedEnvelope);
        var malformedPath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            ProjectFingerprint,
            Guid.NewGuid());
        WriteMalformedEnvelope(malformedPath, malformedContent);
        var expected = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));

        OneshotBootstrapEnvelopeStore.Create(storageRoot, expected);

        Assert.Equal(
            expected,
            OneshotBootstrapEnvelopeStore.Read(
                storageRoot,
                ProjectFingerprint,
                expected.BootstrapId,
                nowUtc));
        Assert.True(File.Exists(malformedPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Read_WhenEnvelopeHasExpired_ThrowsInvalidDataException ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "expired");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(
            storageRoot,
            Guid.NewGuid(),
            nowUtc.AddMinutes(-2),
            nowUtc.AddMinutes(-1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);

        var exception = Assert.Throws<InvalidDataException>(() => OneshotBootstrapEnvelopeStore.Read(
            storageRoot,
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
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            ProjectFingerprint,
            envelope.BootstrapId);
        var tamperedId = Guid.NewGuid();
        File.WriteAllText(
            path.Value,
            File.ReadAllText(path.Value).Replace(
                envelope.BootstrapId.ToString("D"),
                tamperedId.ToString("D"),
                StringComparison.Ordinal));

        var exception = Assert.Throws<InvalidDataException>(() => OneshotBootstrapEnvelopeStore.Read(
            storageRoot,
            ProjectFingerprint,
            envelope.BootstrapId,
            nowUtc));

        Assert.Contains("identifier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Read_WhenStoredParentGenerationWasTampered_ThrowsInvalidDataException ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "tampered-parent-generation");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            ProjectFingerprint,
            envelope.BootstrapId);
        File.WriteAllText(
            path.Value,
            File.ReadAllText(path.Value).Replace(
                $"\"generation\": {envelope.ParentProcess.Generation}",
                $"\"generation\": {envelope.ParentProcess.Generation + 1}",
                StringComparison.Ordinal));

        var exception = Assert.Throws<InvalidDataException>(() => OneshotBootstrapEnvelopeStore.Read(
            storageRoot,
            ProjectFingerprint,
            envelope.BootstrapId,
            nowUtc));

        Assert.Contains("parent process generation", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Read_WhenDifferentIdentifierIsRequested_ThrowsFileNotFoundException ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "different-id");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);

        Assert.Throws<FileNotFoundException>(() => OneshotBootstrapEnvelopeStore.Read(
            storageRoot,
            ProjectFingerprint,
            Guid.NewGuid(),
            nowUtc));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void TryDeleteIfOwned_WhenGenerationDiffers_PreservesCurrentEnvelope ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "ownership");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);
        var differentGeneration = CreateEnvelope(
            storageRoot,
            envelope.BootstrapId,
            nowUtc,
            nowUtc.AddMinutes(2));

        Assert.False(OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(storageRoot, differentGeneration));
        Assert.True(OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(storageRoot, envelope));
    }

    [Theory]
    [InlineData(MalformedEnvelopeContent.InvalidJson)]
    [InlineData(MalformedEnvelopeContent.InvalidUtf8)]
    [Trait("Size", "Medium")]
    public void TryDeleteIfOwned_WhenStoredEnvelopeIsMalformed_ReturnsFalseAndPreservesFile (
        MalformedEnvelopeContent malformedContent)
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "malformed-ownership");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            ProjectFingerprint,
            envelope.BootstrapId);
        WriteMalformedEnvelope(path, malformedContent);

        var deleted = OneshotBootstrapEnvelopeStore.TryDeleteIfOwned(storageRoot, envelope);

        Assert.False(deleted);
        Assert.True(File.Exists(path.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenIdentifierAlreadyExists_DoesNotReplaceGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "duplicate");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);

        Assert.Throws<IOException>(() => OneshotBootstrapEnvelopeStore.Create(
            storageRoot,
            CreateEnvelope(storageRoot, envelope.BootstrapId, nowUtc, nowUtc.AddMinutes(2))));
        Assert.Equal(
            envelope,
            OneshotBootstrapEnvelopeStore.Read(
                storageRoot,
                ProjectFingerprint,
                envelope.BootstrapId,
                nowUtc));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenExpiredEnvelopeUsesForeignFileName_PreservesForeignFile ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "foreign-maintenance-name");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var expiredEnvelope = CreateEnvelope(
            storageRoot,
            Guid.NewGuid(),
            nowUtc.AddMinutes(-2),
            nowUtc.AddMinutes(-1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, expiredEnvelope);
        var ownedPath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            ProjectFingerprint,
            expiredEnvelope.BootstrapId);
        var foreignPath = Path.Combine(Path.GetDirectoryName(ownedPath.Value)!, "foreign.json");
        File.Move(ownedPath.Value, foreignPath);

        OneshotBootstrapEnvelopeStore.Create(
            storageRoot,
            CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1)));

        Assert.True(File.Exists(foreignPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenMaintenanceBudgetIsConsumedByForeignFiles_DoesNotScanPastBudget ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "bounded-maintenance");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var directoryPath = UcliStoragePathResolver.ResolveOneshotBootstrapDirectory(
            storageRoot,
            ProjectFingerprint);
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        WriteForeignMaintenanceFiles(directoryPath);

        var expiredBootstrapId = Guid.Empty;
        var expiredPath = string.Empty;
        for (var attempt = 1; attempt <= 4096; attempt++)
        {
            var candidateBootstrapId = Guid.Parse($"00000000-0000-0000-0000-{attempt:x12}");
            var candidatePath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
                storageRoot,
                ProjectFingerprint,
                candidateBootstrapId);
            File.WriteAllText(candidatePath.Value, "{}");
            var candidateIndex = Array.IndexOf(
                Directory.EnumerateFiles(directoryPath.Value, "*.json", SearchOption.TopDirectoryOnly).ToArray(),
                candidatePath.Value);
            if (candidateIndex >= 128)
            {
                expiredBootstrapId = candidateBootstrapId;
                expiredPath = candidatePath.Value;
                break;
            }

            File.Delete(candidatePath.Value);
        }

        Assert.NotEqual(Guid.Empty, expiredBootstrapId);
        using (var envelopeScope = TestDirectories.CreateTempScope(
                   "oneshot-bootstrap-envelope",
                   "bounded-maintenance-envelope"))
        {
            var envelopeStorageRoot = AbsolutePath.Parse(envelopeScope.FullPath);
            var expiredEnvelope = CreateEnvelope(
                envelopeStorageRoot,
                expiredBootstrapId,
                nowUtc.AddMinutes(-2),
                nowUtc.AddMinutes(-1));
            OneshotBootstrapEnvelopeStore.Create(envelopeStorageRoot, expiredEnvelope);
            File.WriteAllBytes(
                expiredPath,
                File.ReadAllBytes(UcliStoragePathResolver.ResolveOneshotBootstrapPath(
                    envelopeStorageRoot,
                    ProjectFingerprint,
                    expiredBootstrapId).Value));
        }

        OneshotBootstrapEnvelopeStore.Create(
            storageRoot,
            CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1)));

        Assert.True(File.Exists(expiredPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Create_WhenExpiredEnvelopeIdentifierDoesNotMatchFileName_PreservesUnprovenFile ()
    {
        using var scope = TestDirectories.CreateTempScope("oneshot-bootstrap-envelope", "mismatched-maintenance-id");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var nowUtc = DateTimeOffset.UtcNow;
        var expiredEnvelope = CreateEnvelope(
            storageRoot,
            Guid.NewGuid(),
            nowUtc.AddMinutes(-2),
            nowUtc.AddMinutes(-1));
        OneshotBootstrapEnvelopeStore.Create(storageRoot, expiredEnvelope);
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            ProjectFingerprint,
            expiredEnvelope.BootstrapId);
        File.WriteAllText(
            path.Value,
            File.ReadAllText(path.Value).Replace(
                expiredEnvelope.BootstrapId.ToString("D"),
                Guid.NewGuid().ToString("D"),
                StringComparison.Ordinal));

        OneshotBootstrapEnvelopeStore.Create(
            storageRoot,
            CreateEnvelope(storageRoot, Guid.NewGuid(), nowUtc, nowUtc.AddMinutes(1)));

        Assert.True(File.Exists(path.Value));
    }

    private static IpcOneshotBootstrapEnvelope CreateEnvelope (
        AbsolutePath storageRoot,
        Guid bootstrapId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset exitDeadlineUtc)
    {
        return new IpcOneshotBootstrapEnvelope(
            BootstrapId: bootstrapId,
            ParentProcess: ProcessLivenessProbe.CaptureCurrentProcess(),
            ProjectFingerprint: ProjectFingerprint,
            SessionToken: IpcSessionToken.CreateRandom(),
            CreatedAtUtc: createdAtUtc,
            ExitDeadlineUtc: exitDeadlineUtc,
            Endpoint: UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, ProjectFingerprint).Contract);
    }

    private static void WriteForeignMaintenanceFiles (AbsolutePath directoryPath)
    {
        for (var index = 0; index < 128; index++)
        {
            File.WriteAllText(
                Path.Combine(directoryPath.Value, $"000-invalid-{index:D3}.json"),
                "{}");
        }
    }

    private static void WriteMalformedEnvelope (
        AbsolutePath path,
        MalformedEnvelopeContent malformedContent)
    {
        switch (malformedContent)
        {
            case MalformedEnvelopeContent.InvalidJson:
                File.WriteAllText(path.Value, "{");
                break;
            case MalformedEnvelopeContent.InvalidUtf8:
                File.WriteAllBytes(path.Value, new byte[] { 0xC3, 0x28 });
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
