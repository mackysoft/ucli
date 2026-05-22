using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Project;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CompileUnityIpcMethodHandlerTests
    {
        private const string ProjectFingerprint = "project-fingerprint";
        private const string RequestPayloadHash = "request-payload-hash";

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenOnlyTimeoutDiffers_ReturnsSameHash ()
        {
            var handler = CreateHandler();
            var firstRequest = CreateCompileRequest("req-compile-hash-1", "run-hash", timeoutMilliseconds: 1000);
            var secondRequest = CreateCompileRequest("req-compile-hash-2", "run-hash", timeoutMilliseconds: 2000);

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
            Assert.That(firstHash, Is.Not.Null.And.Not.Empty);
            Assert.That(secondHash, Is.EqualTo(firstHash));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenRunIdDiffers_ReturnsDifferentHash ()
        {
            var handler = CreateHandler();
            var firstRequest = CreateCompileRequest("req-compile-hash-1", "run-hash-1");
            var secondRequest = CreateCompileRequest("req-compile-hash-2", "run-hash-2");

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
        public void TryCreateRecoverableRequestPayloadHash_WhenRunIdIsInvalid_ReturnsInvalidArgument ()
        {
            var handler = CreateHandler();
            var request = CreateCompileRequest("req-compile-invalid-run", "../run");

            var result = handler.TryCreateRecoverableRequestPayloadHash(
                request,
                out var requestPayloadHash,
                out var errorResponse);

            Assert.That(result, Is.False);
            Assert.That(requestPayloadHash, Is.Null);
            Assert.That(errorResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(errorResponse.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateRecoverableRequestPayloadHash_WhenTimeoutIsInvalid_ReturnsInvalidArgument ()
        {
            var handler = CreateHandler();
            var request = CreateCompileRequest("req-compile-invalid-timeout", "run-hash", timeoutMilliseconds: 0);

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
            var runId = $"recoverable-{Guid.NewGuid():N}";
            var artifactsDirectory = ResolveArtifactsDirectory(runId);
            try
            {
                var request = CreateCompileRequest("req-compile-recover", runId);
                var pendingSummary = CreatePendingSummary(runId);
                Directory.CreateDirectory(artifactsDirectory);
                var staleSummary = pendingSummary with
                {
                    Completed = true,
                    StartedAtUtc = pendingSummary.StartedAtUtc.AddMinutes(-10),
                    CompletedAtUtc = pendingSummary.StartedAtUtc.AddMinutes(-9),
                };
                File.WriteAllText(
                    Path.Combine(artifactsDirectory, UcliStoragePathNames.CompileSummaryFileName),
                    JsonSerializer.Serialize(staleSummary, IpcJsonSerializerOptions.Default));
                var context = new RecoverableIpcOperationContext(
                    new StubRecoverableIpcOperationStore(),
                    IpcMethodNames.Compile,
                    request.RequestId,
                    RequestPayloadHash,
                    new RecoverableIpcOperationRecord
                    {
                        State = RecoverableIpcOperationStateNames.Pending,
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
            string requestId,
            string runId,
            int timeoutMilliseconds = 5000)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "session-token",
                Method: IpcMethodNames.Compile,
                Payload: IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(runId)
                {
                    TimeoutMilliseconds = timeoutMilliseconds,
                }));
        }

        private static CompileUnityIpcMethodHandler CreateHandler ()
        {
            return new CompileUnityIpcMethodHandler(
                new StubUnityEditorReadinessGate(),
                new IpcProjectIdentity(
                    UnityProjectPathResolver.ResolveProjectRootPath(),
                    ProjectFingerprint,
                    "6000.1.4f1"),
                new StubServerVersionProvider("1.2.3"));
        }

        private static IpcCompileSummary CreatePendingSummary (string runId)
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

        private static string ResolveArtifactsDirectory (string runId)
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
            public bool TryRead (
                string method,
                string requestId,
                string requestPayloadHash,
                out RecoverableIpcOperationRecord record,
                out string errorMessage)
            {
                record = null;
                errorMessage = null;
                return false;
            }

            public bool TryWritePending (
                string method,
                string requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                JsonElement recoveryPayload,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }

            public bool TryWriteCompleted (
                string method,
                string requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                DateTimeOffset completedAtUtc,
                JsonElement recoveryPayload,
                IpcResponse response,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }

            public bool TryPurgeExpiredRecords (
                DateTimeOffset nowUtc,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }
        }
    }
}
