using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using static MackySoft.Ucli.Unity.Tests.LifecycleExecutionHandlerTestSupport;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class LifecycleExecutionTerminalPublicationBoundaryTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("terminal-publication-boundary");

        private static readonly UnityProjectIdentity Project = new(
            "/project",
            ProjectFingerprint,
            "6000.1.5f1");

        private static readonly LifecycleExecutionHostRegistration Host = new(
            new ProcessIdentity(4200, 123456),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000001"));

        private static readonly UnityEditorGenerationSnapshot StartedGeneration =
            new(10, 20, 30, 40);

        [Test]
        [Category("Size.Small")]
        public async Task PublishAsync_WhenFixedRecordCannotBePublished_ReturnsEvidenceAndRecoveryCompletesSameRecord ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var start = await RegisterAsync(executionStore);
            var terminalPath = executionStore.Paths.ResolveTerminalRecordPath(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id).Target;
            WriteGuardedText(terminalPath, "{}");
            var expectedRecord = CreateTerminalRecord(start);
            var boundary = CreateBoundary(executionStore);

            var publication = await boundary.PublishAsync(
                start.LifecycleExecutionRef.Id,
                start.LifecycleExecutionRef,
                _ => expectedRecord,
                CancellationToken.None);

            var failed = AssertType<LifecycleExecutionTerminalPublication<
                RefreshLifecycleExecutionTerminalRecord>.PublicationFailed>(
                publication);
            Assert.That(failed.TerminalRecord, Is.EqualTo(expectedRecord));
            AssertPublishingRecoveryReference(
                failed.ReconnectableReference,
                start);

            DeleteGuardedFileIfExists(terminalPath);
            var recovered = await boundary.RecoverAsync(
                start.LifecycleExecutionRef.Id,
                failed.ReconnectableReference,
                CancellationToken.None);

            var verified = AssertType<LifecycleExecutionTerminalPublication<
                RefreshLifecycleExecutionTerminalRecord>.Verified>(recovered);
            Assert.That(verified.TerminalRecord, Is.EqualTo(expectedRecord));
            Assert.That(
                verified.TerminalReference.Lifecycle,
                Is.EqualTo(ExecutionLifecycle.Terminal));
        }

        [Test]
        [Category("Size.Small")]
        public async Task PublishAsync_WhenExecutionCannotBeRead_ReturnsOnlyReconnectablePublishingReference ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var start = await RegisterAsync(executionStore);
            var recordPath = executionStore.Paths.ResolveRecordPath(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id);
            WriteGuardedText(recordPath, "{");
            var boundary = CreateBoundary(executionStore);

            var publication = await boundary.PublishAsync(
                start.LifecycleExecutionRef.Id,
                start.LifecycleExecutionRef,
                _ => CreateTerminalRecord(start),
                CancellationToken.None);

            var unavailable = AssertType<LifecycleExecutionTerminalPublication<
                RefreshLifecycleExecutionTerminalRecord>.Unavailable>(
                publication);
            AssertPublishingRecoveryReference(
                unavailable.ReconnectableReference,
                start);
        }

        private static LifecycleExecutionTerminalPublicationBoundary<
            RefreshLifecycleExecutionTerminalRecord> CreateBoundary (
            FileLifecycleExecutionStore executionStore)
        {
            return new LifecycleExecutionTerminalPublicationBoundary<
                RefreshLifecycleExecutionTerminalRecord>(
                LifecycleExecutionKind.Refresh,
                executionStore,
                NoOpDaemonLogger.Instance,
                "Refresh terminal publication failed.",
                "Refresh terminal publication failed during recovery.");
        }

        private static async Task<LifecycleExecutionStartBinding> RegisterAsync (
            FileLifecycleExecutionStore executionStore)
        {
            var definition = new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Refresh);
            var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            var result = await executionStore.StartAsync(
                definition,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                Project,
                Host,
                StartedGeneration,
                startedAtUtc.AddMinutes(5),
                startedAtUtc,
                CancellationToken.None);
            Assert.That(result.IsSuccess, Is.True);
            return result.Binding;
        }

        private static RefreshLifecycleExecutionTerminalRecord
            CreateTerminalRecord (LifecycleExecutionStartBinding start)
        {
            return new RefreshLifecycleExecutionTerminalRecord(
                start.LifecycleExecutionRef.Id,
                start.LifecycleExecutionRef.DefinitionDigest,
                start.Project,
                start.Host,
                start.StartedGeneration,
                terminalGeneration: null,
                start.DeadlineUtc,
                start.StartedAtUtc,
                start.DeadlineUtc,
                LifecycleExecutionTerminalReason.DeadlineExceeded,
                ExecutionApplicationState.NotApplied,
                result: null,
                verdict: null,
                Array.Empty<ArtifactRef>());
        }

        private static void AssertPublishingRecoveryReference (
            ExecutionRef executionReference,
            LifecycleExecutionStartBinding start)
        {
            Assert.That(executionReference.Id, Is.EqualTo(
                start.LifecycleExecutionRef.Id));
            Assert.That(
                executionReference.DefinitionDigest,
                Is.EqualTo(start.LifecycleExecutionRef.DefinitionDigest));
            Assert.That(
                executionReference.Lifecycle,
                Is.EqualTo(ExecutionLifecycle.Recovery));
            Assert.That(
                executionReference.State.Value,
                Is.EqualTo(TextVocabulary.GetText(
                    LifecycleExecutionState.Publishing)));
        }

        private static T AssertType<T> (object value)
        {
            Assert.That(value, Is.TypeOf<T>());
            return (T)value;
        }
    }
}
