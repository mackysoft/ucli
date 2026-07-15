using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonBootstrapSessionTokenResolverTests
    {
        private const string FirstSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        private const string ReplacementSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAE";

        private const string NonCanonicalSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB";

        private static readonly ProjectFingerprint ProjectFingerprint = new ProjectFingerprint(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        private static readonly ProjectFingerprint OtherProjectFingerprint = new ProjectFingerprint(
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        private static readonly DateTimeOffset SessionIssuedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        private static readonly Guid SessionGenerationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid SuccessorSessionGenerationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly IpcEndpoint Endpoint = new IpcEndpoint(
            IpcTransportKind.NamedPipe,
            "ucli-daemon-bootstrap-session-token-resolver-tests");

        [Test]
        [Category("Size.Small")]
        public async Task ResolveAsync_WhenSessionFileIsReplacedBySuccessorGeneration_PinsInitialToken ()
        {
            var storageRoot = CreateTemporaryStorageRoot();
            try
            {
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, ProjectFingerprint);
                WriteSessionJson(
                    sessionPath,
                    CreateSessionContract(SessionGenerationId, FirstSessionToken, SessionIssuedAtUtc));

                var sessionToken = await DaemonBootstrapSessionTokenResolver.ResolveAsync(
                    CreateBootstrapArguments(storageRoot, sessionPath, SessionGenerationId),
                    CancellationToken.None);
                var validator = new ExactSessionTokenValidator(sessionToken);

                WriteSessionJson(
                    sessionPath,
                    CreateSessionContract(
                        SuccessorSessionGenerationId,
                        ReplacementSessionToken,
                        SessionIssuedAtUtc.AddSeconds(1)));

                Assert.That(
                    await validator.ValidateAsync(ParseSessionToken(FirstSessionToken), CancellationToken.None),
                    Is.True);
                Assert.That(
                    await validator.ValidateAsync(ParseSessionToken(ReplacementSessionToken), CancellationToken.None),
                    Is.False);
            }
            finally
            {
                DeleteTemporaryStorageRoot(storageRoot);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveAsync_WhenOnlyPersistedTokenChanges_PinsInitialToken ()
        {
            var storageRoot = CreateTemporaryStorageRoot();
            try
            {
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, ProjectFingerprint);
                WriteSessionJson(
                    sessionPath,
                    CreateSessionContract(SessionGenerationId, FirstSessionToken, SessionIssuedAtUtc));

                var sessionToken = await DaemonBootstrapSessionTokenResolver.ResolveAsync(
                    CreateBootstrapArguments(storageRoot, sessionPath, SessionGenerationId),
                    CancellationToken.None);
                var validator = new ExactSessionTokenValidator(sessionToken);

                WriteSessionJson(
                    sessionPath,
                    CreateSessionContract(SessionGenerationId, ReplacementSessionToken, SessionIssuedAtUtc));

                Assert.That(
                    await validator.ValidateAsync(ParseSessionToken(FirstSessionToken), CancellationToken.None),
                    Is.True);
                Assert.That(
                    await validator.ValidateAsync(ParseSessionToken(ReplacementSessionToken), CancellationToken.None),
                    Is.False);
            }
            finally
            {
                DeleteTemporaryStorageRoot(storageRoot);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveAsync_WhenSessionIsReplacedBeforeReadBySuccessorWithSameTimestampAndEndpoint_RejectsSuccessor ()
        {
            var storageRoot = CreateTemporaryStorageRoot();
            try
            {
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, ProjectFingerprint);
                WriteSessionJson(
                    sessionPath,
                    CreateSessionContract(SessionGenerationId, FirstSessionToken, SessionIssuedAtUtc));
                var bootstrapArguments = CreateBootstrapArguments(
                    storageRoot,
                    sessionPath,
                    SessionGenerationId);

                WriteSessionJson(
                    sessionPath,
                    CreateSessionContract(
                        SuccessorSessionGenerationId,
                        ReplacementSessionToken,
                        SessionIssuedAtUtc));

                Assert.ThrowsAsync<InvalidDataException>(() =>
                    DaemonBootstrapSessionTokenResolver.ResolveAsync(
                        bootstrapArguments,
                        CancellationToken.None));
            }
            finally
            {
                DeleteTemporaryStorageRoot(storageRoot);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveAsync_WhenSessionPathIsNotCanonical_RejectsBootstrapArguments ()
        {
            var storageRoot = CreateTemporaryStorageRoot();
            try
            {
                var canonicalSessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, ProjectFingerprint);
                var replacedSessionPath = Path.Combine(storageRoot, "replacement", "session.json");
                var contract = CreateSessionContract(SessionGenerationId, FirstSessionToken, SessionIssuedAtUtc);
                WriteSessionJson(canonicalSessionPath, contract);
                WriteSessionJson(replacedSessionPath, contract);

                Assert.ThrowsAsync<InvalidOperationException>(() =>
                    DaemonBootstrapSessionTokenResolver.ResolveAsync(
                        CreateBootstrapArguments(storageRoot, replacedSessionPath, SessionGenerationId),
                        CancellationToken.None));
            }
            finally
            {
                DeleteTemporaryStorageRoot(storageRoot);
            }
        }

        [TestCase(SessionGenerationMismatch.SchemaVersion)]
        [TestCase(SessionGenerationMismatch.SessionGenerationId)]
        [TestCase(SessionGenerationMismatch.ProjectFingerprint)]
        [TestCase(SessionGenerationMismatch.IssuedAtUtc)]
        [TestCase(SessionGenerationMismatch.IssuedAtUtcOffset)]
        [TestCase(SessionGenerationMismatch.EditorMode)]
        [TestCase(SessionGenerationMismatch.OwnerKind)]
        [TestCase(SessionGenerationMismatch.CanShutdownProcess)]
        [TestCase(SessionGenerationMismatch.EndpointTransportKind)]
        [TestCase(SessionGenerationMismatch.EndpointAddress)]
        [Category("Size.Small")]
        public void ResolveAsync_WhenPersistedGenerationDoesNotMatch_RejectsSession (
            SessionGenerationMismatch mismatch)
        {
            var storageRoot = CreateTemporaryStorageRoot();
            try
            {
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, ProjectFingerprint);
                var contract = ApplyMismatch(
                    CreateSessionContract(SessionGenerationId, FirstSessionToken, SessionIssuedAtUtc),
                    mismatch);
                WriteSessionJson(sessionPath, contract);

                Assert.ThrowsAsync<InvalidDataException>(() =>
                    DaemonBootstrapSessionTokenResolver.ResolveAsync(
                        CreateBootstrapArguments(storageRoot, sessionPath, SessionGenerationId),
                        CancellationToken.None));
            }
            finally
            {
                DeleteTemporaryStorageRoot(storageRoot);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveAsync_WhenPersistedTokenIsNonCanonical_RejectsSession ()
        {
            var storageRoot = CreateTemporaryStorageRoot();
            try
            {
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, ProjectFingerprint);
                WriteSessionJson(
                    sessionPath,
                    CreateSessionContract(SessionGenerationId, NonCanonicalSessionToken, SessionIssuedAtUtc));

                Assert.ThrowsAsync<InvalidDataException>(() =>
                    DaemonBootstrapSessionTokenResolver.ResolveAsync(
                        CreateBootstrapArguments(storageRoot, sessionPath, SessionGenerationId),
                        CancellationToken.None));
            }
            finally
            {
                DeleteTemporaryStorageRoot(storageRoot);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveAsync_WhenSessionFileExceedsStorageLimit_RejectsSession ()
        {
            var storageRoot = CreateTemporaryStorageRoot();
            try
            {
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, ProjectFingerprint);
                Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
                File.WriteAllText(
                    sessionPath,
                    $"{{\"sessionToken\":\"{FirstSessionToken}\",\"padding\":\"{new string('a', DaemonSessionStorageContract.MaximumFileSizeBytes)}\"}}");

                Assert.ThrowsAsync<IOException>(() =>
                    DaemonBootstrapSessionTokenResolver.ResolveAsync(
                        CreateBootstrapArguments(storageRoot, sessionPath, SessionGenerationId),
                        CancellationToken.None));
            }
            finally
            {
                DeleteTemporaryStorageRoot(storageRoot);
            }
        }

        private static DaemonSessionJsonContract ApplyMismatch (
            DaemonSessionJsonContract contract,
            SessionGenerationMismatch mismatch)
        {
            return mismatch switch
            {
                SessionGenerationMismatch.SchemaVersion => contract with
                {
                    SchemaVersion = DaemonSessionStorageContract.CurrentSchemaVersion + 1,
                },
                SessionGenerationMismatch.SessionGenerationId => contract with
                {
                    SessionGenerationId = SuccessorSessionGenerationId,
                },
                SessionGenerationMismatch.ProjectFingerprint => contract with
                {
                    ProjectFingerprint = OtherProjectFingerprint,
                },
                SessionGenerationMismatch.IssuedAtUtc => contract with
                {
                    IssuedAtUtc = SessionIssuedAtUtc.AddSeconds(1),
                },
                SessionGenerationMismatch.IssuedAtUtcOffset => contract with
                {
                    IssuedAtUtc = SessionIssuedAtUtc.ToOffset(TimeSpan.FromHours(-8)),
                },
                SessionGenerationMismatch.EditorMode => contract with
                {
                    EditorMode = DaemonEditorMode.Gui,
                },
                SessionGenerationMismatch.OwnerKind => contract with
                {
                    OwnerKind = DaemonSessionOwnerKind.User,
                },
                SessionGenerationMismatch.CanShutdownProcess => contract with
                {
                    CanShutdownProcess = false,
                },
                SessionGenerationMismatch.EndpointTransportKind => contract with
                {
                    EndpointTransportKind = IpcTransportKind.UnixDomainSocket,
                    EndpointAddress = "/tmp/ucli-daemon-bootstrap-session-token-resolver-tests.sock",
                },
                SessionGenerationMismatch.EndpointAddress => contract with
                {
                    EndpointAddress = "ucli-daemon-bootstrap-session-token-resolver-tests-other",
                },
                _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null),
            };
        }

        private static DaemonSessionJsonContract CreateSessionContract (
            Guid sessionGenerationId,
            string sessionToken,
            DateTimeOffset issuedAtUtc)
        {
            return new DaemonSessionJsonContract(
                SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                SessionGenerationId: sessionGenerationId,
                SessionToken: sessionToken,
                ProjectFingerprint: ProjectFingerprint,
                IssuedAtUtc: issuedAtUtc,
                EditorMode: DaemonEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: true,
                EndpointTransportKind: Endpoint.TransportKind,
                EndpointAddress: Endpoint.Address,
                ProcessId: null,
                ProcessStartedAtUtc: null,
                OwnerProcessId: 1234,
                EditorInstanceId: null);
        }

        private static void WriteSessionJson (
            string sessionPath,
            DaemonSessionJsonContract contract)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
            File.WriteAllText(
                sessionPath,
                DaemonSessionJsonContractSerializer.Serialize(contract) + Environment.NewLine);
        }

        private static IpcDaemonBootstrapArguments CreateBootstrapArguments (
            string storageRoot,
            string sessionPath,
            Guid sessionGenerationId)
        {
            return new IpcDaemonBootstrapArguments(
                RepositoryRoot: storageRoot,
                ProjectFingerprint: ProjectFingerprint,
                SessionPath: sessionPath,
                SessionGenerationId: sessionGenerationId,
                SessionIssuedAtUtc: SessionIssuedAtUtc,
                Endpoint: Endpoint);
        }

        private static IpcSessionToken ParseSessionToken (string value)
        {
            Assert.That(IpcSessionToken.TryParse(value, out var sessionToken), Is.True);
            return sessionToken!;
        }

        private static string CreateTemporaryStorageRoot ()
        {
            var storageRoot = Path.Combine(
                Path.GetTempPath(),
                "ucli-daemon-bootstrap-session-token-resolver-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storageRoot);
            return storageRoot;
        }

        private static void DeleteTemporaryStorageRoot (string storageRoot)
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }

        public enum SessionGenerationMismatch
        {
            SchemaVersion,
            SessionGenerationId,
            ProjectFingerprint,
            IssuedAtUtc,
            IssuedAtUtcOffset,
            EditorMode,
            OwnerKind,
            CanShutdownProcess,
            EndpointTransportKind,
            EndpointAddress,
        }
    }
}
