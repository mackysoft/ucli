using System;
using System.Collections;
using System.IO;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Project;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CompileUnityIpcMethodHandlerTests
    {
        private const string ProjectFingerprint = "project-fingerprint";

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
                var context = new RecoverableIpcOperationContext(
                    new StubRecoverableIpcOperationStore(),
                    IpcMethodNames.Compile,
                    request.RequestId,
                    new RecoverableIpcOperationRecord
                    {
                        State = RecoverableIpcOperationStateNames.Pending,
                        StartedAtUtc = pendingSummary.StartedAtUtc,
                        RecoveryPayload = IpcPayloadCodec.SerializeToElement(pendingSummary),
                    });
                var handler = new CompileUnityIpcMethodHandler(
                    new StubUnityEditorReadinessGate(),
                    new IpcProjectIdentity(
                        UnityProjectPathResolver.ResolveProjectRootPath(),
                        ProjectFingerprint,
                        "6000.1.4f1"),
                    new StubServerVersionProvider("1.2.3"));

                var response = await handler.HandleRecoverableAsync(request, context, CancellationToken.None);

                Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
                Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcCompileResponse payload, out _), Is.True);
                Assert.That(payload.RunId, Is.EqualTo(runId));
                Assert.That(payload.Summary.Completed, Is.True);
                Assert.That(payload.Summary.DomainReload.ReloadObserved, Is.True);
                Assert.That(payload.Summary.ScriptCompilation.Completed, Is.True);
                Assert.That(File.Exists(Path.Combine(artifactsDirectory, UcliStoragePathNames.CompileSummaryFileName)), Is.True);
                Assert.That(File.Exists(Path.Combine(artifactsDirectory, UcliStoragePathNames.CompileDiagnosticsFileName)), Is.True);
            }
            finally
            {
                DeleteDirectoryIfExists(artifactsDirectory);
            }
        });

        private static IpcRequest CreateCompileRequest (
            string requestId,
            string runId)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "session-token",
                Method: IpcMethodNames.Compile,
                Payload: IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(runId)
                {
                    TimeoutMilliseconds = 5000,
                }));
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
                DateTimeOffset startedAtUtc,
                DateTimeOffset completedAtUtc,
                JsonElement recoveryPayload,
                IpcResponse response,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }

            public bool TryPurgeExpiredCompletedRecords (
                DateTimeOffset nowUtc,
                out string errorMessage)
            {
                errorMessage = null;
                return true;
            }
        }
    }
}
