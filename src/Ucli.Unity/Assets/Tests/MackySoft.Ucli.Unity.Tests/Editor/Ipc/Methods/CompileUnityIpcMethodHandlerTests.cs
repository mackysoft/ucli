using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Project;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CompileUnityIpcMethodHandlerTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("project-fingerprint");

        private static readonly Sha256Digest RequestPayloadHash = Sha256Digest.Parse(
            "cda34040abc54e9b351b66c6ecbc9708cf2c70996b0805553b3854bdce80d94b");

        private static readonly Guid RunId = Guid.Parse("00000000-0000-0000-0000-000000000606");

        private static readonly Guid OtherRunId = Guid.Parse("00000000-0000-0000-0000-000000000607");

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenOnlyTimeoutDiffers_ReturnsSameHash ()
        {
            var handler = CreateHandler();
            var firstRequest = CreateCompileRequest(Guid.NewGuid(), RunId, timeoutMilliseconds: 1000);
            var secondRequest = CreateCompileRequest(Guid.NewGuid(), RunId, timeoutMilliseconds: 2000);

            var firstResult = handler.TryCreateRecoverableRequestPayloadHash(
                firstRequest,
                out var firstHash,
                out var firstError);
            var secondResult = handler.TryCreateRecoverableRequestPayloadHash(
                secondRequest,
                out var secondHash,
                out var secondError);

            Assert.That(firstResult, Is.True, firstError?.Errors[0].Message);
            Assert.That(secondResult, Is.True, secondError?.Errors[0].Message);
            Assert.That(firstHash, Is.Not.Null);
            Assert.That(secondHash, Is.EqualTo(firstHash));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenRunIdDiffers_ReturnsDifferentHash ()
        {
            var handler = CreateHandler();
            var firstRequest = CreateCompileRequest(Guid.NewGuid(), RunId);
            var secondRequest = CreateCompileRequest(Guid.NewGuid(), OtherRunId);

            var firstResult = handler.TryCreateRecoverableRequestPayloadHash(
                firstRequest,
                out var firstHash,
                out var firstError);
            var secondResult = handler.TryCreateRecoverableRequestPayloadHash(
                secondRequest,
                out var secondHash,
                out var secondError);

            Assert.That(firstResult, Is.True, firstError?.Errors[0].Message);
            Assert.That(secondResult, Is.True, secondError?.Errors[0].Message);
            Assert.That(secondHash, Is.Not.EqualTo(firstHash));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenTimeoutIsInvalid_ReturnsInvalidArgument ()
        {
            var handler = CreateHandler();
            var request = CreateCompileRequest(Guid.NewGuid(), RunId, timeoutMilliseconds: 0);

            var result = handler.TryCreateRecoverableRequestPayloadHash(
                request,
                out var requestPayloadHash,
                out var errorResponse);

            Assert.That(result, Is.False);
            Assert.That(requestPayloadHash, Is.Null);
            Assert.That(errorResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(errorResponse.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleRecoverableAsync_WhenPendingSummaryObservedDomainReload_CompletesSummaryAndResponse () => UniTask.ToCoroutine(async () =>
        {
            var runId = Guid.NewGuid();
            var artifactsDirectory = ResolveArtifactsDirectory(runId);
            try
            {
                var request = CreateCompileRequest(Guid.NewGuid(), runId);
                var pendingSummary = CreatePendingSummary(runId);
                Directory.CreateDirectory(artifactsDirectory);
                var staleStartedAtUtc = pendingSummary.StartedAtUtc.AddMinutes(-10);
                var staleSummary = new IpcCompileSummary(
                    RunId: pendingSummary.RunId,
                    ProjectFingerprint: pendingSummary.ProjectFingerprint,
                    Completed: true,
                    StartedAtUtc: staleStartedAtUtc,
                    CompletedAtUtc: staleStartedAtUtc.AddMinutes(1),
                    Refresh: pendingSummary.Refresh,
                    ScriptCompilation: pendingSummary.ScriptCompilation,
                    DomainReload: pendingSummary.DomainReload,
                    Lifecycle: pendingSummary.Lifecycle);
                File.WriteAllText(
                    Path.Combine(artifactsDirectory, UcliStoragePathNames.CompileSummaryFileName),
                    JsonSerializer.Serialize(staleSummary, IpcJsonSerializerOptions.Default));
                var context = new RecoverableIpcOperationContext(
                    new StubRecoverableIpcOperationStore(),
                    UnityIpcMethod.Compile,
                    request.RequestId,
                    RequestPayloadHash,
                    new RecoverableIpcOperationRecord
                    {
                        State = RecoverableIpcOperationState.Pending,
                        StartedAtUtc = pendingSummary.StartedAtUtc,
                        RecoveryPayload = IpcPayloadCodec.SerializeToElement(pendingSummary),
                    });
                var handler = CreateHandler();

                var response = await handler.HandleRecoverableAsync(request, context, CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcCompileResponse payload, out _), Is.True);
                Assert.That(payload.RunId, Is.EqualTo(runId));
                Assert.That(payload.Summary.Completed, Is.True);
                Assert.That(payload.Summary.StartedAtUtc, Is.EqualTo(pendingSummary.StartedAtUtc));
                Assert.That(payload.Summary.DomainReload.ReloadObserved, Is.True);
                Assert.That(payload.Summary.ScriptCompilation.Completed, Is.True);
                var summaryJsonPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.CompileSummaryFileName);
                Assert.That(File.Exists(summaryJsonPath), Is.True);
                var persistedSummary = JsonSerializer.Deserialize<IpcCompileSummary>(
                    File.ReadAllText(summaryJsonPath),
                    IpcJsonSerializerOptions.Default);
                Assert.That(persistedSummary, Is.Not.Null);
                Assert.That(persistedSummary!.Completed, Is.True);
                Assert.That(persistedSummary.StartedAtUtc, Is.EqualTo(pendingSummary.StartedAtUtc));
                Assert.That(File.Exists(Path.Combine(artifactsDirectory, UcliStoragePathNames.CompileDiagnosticsFileName)), Is.True);
            }
            finally
            {
                DeleteDirectoryIfExists(artifactsDirectory);
            }
        });

        private static IpcRequest CreateCompileRequest (
            Guid requestId,
            Guid runId,
            int timeoutMilliseconds = 5000)
        {
            return new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.Compile),
                payload: IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(runId)
                {
                    TimeoutMilliseconds = timeoutMilliseconds,
                }),
                responseMode: "single");
        }

        private static CompileUnityIpcMethodHandler CreateHandler ()
        {
            return new CompileUnityIpcMethodHandler(
                new StubUnityEditorReadinessGate(),
                new IpcProjectIdentity(
                    UnityProjectPathResolver.ResolveProjectRootPath(),
                    ProjectFingerprint,
                    "6000.1.4f1"),
                new StubServerVersionProvider("1.2.3"),
                NoOpDaemonLogger.Instance);
        }

        private static IpcCompileSummary CreatePendingSummary (Guid runId)
        {
            var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            return new IpcCompileSummary(
                RunId: runId,
                ProjectFingerprint: ProjectFingerprint,
                Completed: false,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: null,
                Refresh: new IpcCompileSummary.RefreshEvidence(
                    Origin: "assetDatabaseRefresh",
                    Requested: true,
                    StartedAtUtc: startedAtUtc,
                    CompletedAtUtc: null,
                    Completed: false),
                ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                    Started: false,
                    Completed: false,
                    CompileGenerationBefore: "1",
                    CompileGenerationAfter: "1",
                    Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(0, 0, null)),
                DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                    ReloadRequired: false,
                    ReloadObserved: false,
                    GenerationBefore: "0",
                    GenerationAfter: "0",
                    Settled: false),
                Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                    ServerVersion: "1.2.3",
                    UnityVersion: "6000.1.4f1",
                    EditorMode: "batchmode",
                    LifecycleState: IpcEditorLifecycleStateCodec.Ready,
                    BlockingReason: null,
                    CompileState: IpcCompileStateCodec.Ready,
                    CompileGeneration: "1",
                    DomainReloadGeneration: "0",
                    CanAcceptExecutionRequests: true,
                    ObservedAtUtc: startedAtUtc,
                    ActionRequired: null,
                    PrimaryDiagnostic: null));
        }

        private static string ResolveArtifactsDirectory (Guid runId)
        {
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(UnityProjectPathResolver.ResolveProjectRootPath());
            return UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory(
                storageRoot,
                ProjectFingerprint,
                runId);
        }

        private static void DeleteDirectoryIfExists (string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception exception)
            {
                TestContext.WriteLine($"Temporary compile recovery test directory cleanup failed: {path}. {exception.Message}");
            }
        }

        private sealed class StubServerVersionProvider : IServerVersionProvider
        {
            private readonly string version;

            public StubServerVersionProvider (string version)
            {
                this.version = version;
            }

            public string GetVersion ()
            {
                return version;
            }
        }

        private sealed class StubRecoverableIpcOperationStore : IRecoverableIpcOperationStore
        {
            public ValueTask<RecoverableIpcOperationReadResult> ReadAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<RecoverableIpcOperationReadResult>(
                    RecoverableIpcOperationReadResult.Missing());
            }

            public ValueTask<RecoverableIpcOperationStoreResult> WritePendingAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                DateTimeOffset startedAtUtc,
                JsonElement recoveryPayload,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<RecoverableIpcOperationStoreResult>(
                    RecoverableIpcOperationStoreResult.Success());
            }

            public ValueTask<RecoverableIpcOperationStoreResult> WriteCompletedAsync (
                UnityIpcMethod method,
                Guid requestId,
                Sha256Digest requestPayloadHash,
                DateTimeOffset startedAtUtc,
                DateTimeOffset completedAtUtc,
                JsonElement recoveryPayload,
                IpcResponse response,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<RecoverableIpcOperationStoreResult>(
                    RecoverableIpcOperationStoreResult.Success());
            }

            public string ConsumeMaintenanceFailure ()
            {
                return null;
            }
        }
    }
}
