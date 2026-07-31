using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class LifecycleExecutionAttemptBoundaryTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint = new(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

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
        public async Task ResolveInvocationAsync_WhenDurableStartIsMissing_ReturnsMissing ()
        {
            using var sourceScope = TemporaryStorageScope.Create();
            using var emptyScope = TemporaryStorageScope.Create();
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var requestedStart = await RegisterAsync(
                sourceScope.CreateExecutionStore(ProjectFingerprint),
                startedAtUtc,
                startedAtUtc.AddMinutes(5));
            var boundary = new LifecycleExecutionAttemptBoundary(
                emptyScope.CreateExecutionStore(ProjectFingerprint));

            var result = await boundary.ResolveInvocationAsync(
                LifecycleExecutionKind.Refresh,
                requestedStart,
                CancellationToken.None);

            Assert.That(
                result,
                Is.TypeOf<LifecycleExecutionAttemptResolution.Missing>());
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveInvocationAsync_WhenDurableStartDoesNotMatch_ReturnsExactMismatch ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var established = await RegisterAsync(
                executionStore,
                startedAtUtc,
                startedAtUtc.AddMinutes(5));
            var requestedStart = new LifecycleExecutionStartBinding(
                established.LifecycleExecutionRef,
                new UnityProjectIdentity(
                    "/different-project",
                    ProjectFingerprint,
                    Project.UnityVersion),
                established.Host,
                established.StartedGeneration,
                established.DeadlineUtc,
                established.StartedAtUtc);
            var boundary = new LifecycleExecutionAttemptBoundary(
                executionStore);

            var result = await boundary.ResolveInvocationAsync(
                LifecycleExecutionKind.Refresh,
                requestedStart,
                CancellationToken.None);

            var mismatch = AssertType<
                LifecycleExecutionAttemptResolution.BindingMismatch>(result);
            Assert.That(
                mismatch.Match,
                Is.EqualTo(
                    LifecycleExecutionStartBindingMatch.ProjectMismatch));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveInvocationAsync_WhenExecutionIsOpen_ReturnsDeadlineOnlyAttempt ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var start = await RegisterAsync(
                executionStore,
                startedAtUtc,
                startedAtUtc.AddMinutes(5));
            var expected = await executionStore.ReadAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);
            var boundary = new LifecycleExecutionAttemptBoundary(
                executionStore);

            var result = await boundary.ResolveInvocationAsync(
                LifecycleExecutionKind.Refresh,
                start,
                CancellationToken.None);

            using var open = AssertType<
                LifecycleExecutionAttemptResolution.Open>(result);
            Assert.That(open.Execution, Is.EqualTo(expected));
            Assert.That(
                open.DeadlineCancellationToken.IsCancellationRequested,
                Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ResolveRecoveryAsync_WhenDeadlineElapsed_ReturnsAuthoritativeDeadlineState ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
            var start = await RegisterAsync(
                executionStore,
                startedAtUtc,
                startedAtUtc.AddMinutes(1));
            var expected = await executionStore.ReadAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);
            var boundary = new LifecycleExecutionAttemptBoundary(
                executionStore);

            var result = await boundary.ResolveRecoveryAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);

            var deadline = AssertType<
                LifecycleExecutionAttemptResolution.DeadlineExceeded>(result);
            Assert.That(deadline.Execution, Is.EqualTo(expected));
        }

        [TestCase(true)]
        [TestCase(false)]
        [Category("Size.Small")]
        public async Task ResolveInvocationAsync_WhenTerminalPublicationStartedOrCompleted_BypassesDeadline (
            bool terminal)
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
            var start = await RegisterAsync(
                executionStore,
                startedAtUtc,
                startedAtUtc.AddMinutes(1));
            await PublishTerminalStateAsync(
                executionStore,
                start,
                terminal);
            var expected = await executionStore.ReadAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);
            var boundary = new LifecycleExecutionAttemptBoundary(
                executionStore);

            var result = await boundary.ResolveInvocationAsync(
                LifecycleExecutionKind.Refresh,
                start,
                CancellationToken.None);

            var terminalOrPublishing = AssertType<
                LifecycleExecutionAttemptResolution.TerminalOrPublishing>(
                result);
            Assert.That(
                terminalOrPublishing.Execution,
                Is.EqualTo(expected));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ObserveCompletionAsync_WhenTerminalPublicationWinsDeadlineCancellation_ReturnsTerminalState ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var start = await RegisterAsync(
                executionStore,
                startedAtUtc,
                DateTimeOffset.UtcNow.AddSeconds(1));
            var boundary = new LifecycleExecutionAttemptBoundary(
                executionStore);
            var initial = await boundary.ResolveRecoveryAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);
            using var open = AssertType<
                LifecycleExecutionAttemptResolution.Open>(initial);

            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            Assert.That(
                open.DeadlineCancellationToken.IsCancellationRequested,
                Is.True);
            var publication = await executionStore.PublishTerminalAsync(
                CreateDeadlineTerminalRecord(start),
                CancellationToken.None);
            Assert.That(publication.IsSuccess, Is.True);
            var expected = await executionStore.ReadAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);

            var result = await boundary.ObserveCompletionAsync(
                LifecycleExecutionKind.Refresh,
                open,
                Task.FromCanceled<int>(open.DeadlineCancellationToken));

            var terminal = AssertType<
                LifecycleExecutionAttemptResolution.TerminalOrPublishing>(
                result);
            Assert.That(terminal.Execution, Is.EqualTo(expected));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ObserveCompletionAsync_WhenOnlyCallerCanceled_PropagatesCancellation ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var start = await RegisterAsync(
                executionStore,
                startedAtUtc,
                startedAtUtc.AddMinutes(5));
            var boundary = new LifecycleExecutionAttemptBoundary(
                executionStore);
            var initial = await boundary.ResolveRecoveryAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);
            using var open = AssertType<
                LifecycleExecutionAttemptResolution.Open>(initial);
            using var callerCancellation = new CancellationTokenSource();
            callerCancellation.Cancel();
            var cancellation = new OperationCanceledException(
                callerCancellation.Token);

            var exception = Assert.CatchAsync<OperationCanceledException>(
                async () => await boundary.ObserveCompletionAsync(
                    LifecycleExecutionKind.Refresh,
                    open,
                    Task.FromException<int>(cancellation)));

            Assert.That(
                exception.CancellationToken,
                Is.EqualTo(callerCancellation.Token));
            Assert.That(
                open.DeadlineCancellationToken.IsCancellationRequested,
                Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public async Task ObserveCompletionAsync_WhenActionOperationCompletes_ReturnsOpaqueActionResult ()
        {
            using var scope = TemporaryStorageScope.Create();
            var executionStore = scope.CreateExecutionStore(ProjectFingerprint);
            var startedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            var start = await RegisterAsync(
                executionStore,
                startedAtUtc,
                startedAtUtc.AddMinutes(5));
            var boundary = new LifecycleExecutionAttemptBoundary(
                executionStore);
            var initial = await boundary.ResolveRecoveryAsync(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id,
                CancellationToken.None);
            using var open = AssertType<
                LifecycleExecutionAttemptResolution.Open>(initial);

            var result = await boundary.ObserveCompletionAsync(
                LifecycleExecutionKind.Refresh,
                open,
                Task.FromResult("action-result"));

            var completed = AssertType<
                LifecycleExecutionAttemptResolution.Completed<string>>(result);
            Assert.That(completed.Result, Is.EqualTo("action-result"));
        }

        private static async Task<LifecycleExecutionStartBinding> RegisterAsync (
            FileLifecycleExecutionStore executionStore,
            DateTimeOffset startedAtUtc,
            DateTimeOffset deadlineUtc)
        {
            var definition = new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Refresh);
            var result = await executionStore.StartAsync(
                definition,
                Guid.NewGuid(),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                Project,
                Host,
                StartedGeneration,
                deadlineUtc,
                startedAtUtc,
                CancellationToken.None);
            Assert.That(result.IsSuccess, Is.True);
            return result.Binding;
        }

        private static async Task PublishTerminalStateAsync (
            FileLifecycleExecutionStore executionStore,
            LifecycleExecutionStartBinding start,
            bool terminal)
        {
            var artifactRefs = terminal
                ? Array.Empty<ArtifactRef>()
                : new ArtifactRef[]
                {
                    new PathArtifactRef(
                        LifecycleExecutionArtifactContract.TerminalRecordKind,
                        LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                        executionStore.Paths.CreateTerminalRecordArtifactPath(
                            LifecycleExecutionKind.Refresh,
                            start.LifecycleExecutionRef.Id),
                        Sha256Digest.Compute(ReadOnlySpan<byte>.Empty),
                        sizeBytes: 0,
                        start.StartedAtUtc),
                };
            var publication = await executionStore.PublishTerminalAsync(
                CreateDeadlineTerminalRecord(start, artifactRefs),
                CancellationToken.None);
            Assert.That(
                publication.Outcome,
                terminal
                    ? Is.EqualTo(
                        LifecycleExecutionTerminalPublicationOutcome.Published)
                    : Is.EqualTo(
                        LifecycleExecutionTerminalPublicationOutcome
                            .PublicationFailed));
        }

        private static RefreshLifecycleExecutionTerminalRecord
            CreateDeadlineTerminalRecord (
            LifecycleExecutionStartBinding start,
            IReadOnlyList<ArtifactRef> artifactRefs = null)
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
                artifactRefs ?? Array.Empty<ArtifactRef>());
        }

        private static T AssertType<T> (object value)
        {
            Assert.That(value, Is.TypeOf<T>());
            return (T)value;
        }
    }
}
