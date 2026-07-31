using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class RefreshUnityIpcMethodHandlerTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithValidPayload_DelegatesTypedStartAndEncodesOutcome ()
        {
            var start = CreateStart();
            var expectedCode = UcliCoreErrorCodes.InternalError;
            var executionHandler = new StubRefreshLifecycleExecutionHandler(
                RefreshLifecycleExecutionOutcome.Failed(
                    start.Project,
                    expectedCode,
                    "Refresh execution failed.",
                    lifecycleExecutionRef: null,
                    ExecutionApplicationState.NotApplied,
                    result: null,
                    refresh: null,
                    observedLifecycle: null,
                    readPostcondition: null,
                    hasActionPayload: false));
            var handler = new RefreshUnityIpcMethodHandler(
                executionHandler,
                NoOpDaemonLogger.Instance);

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                handler,
                CreateRequest(start),
                CancellationToken.None);

            Assert.That(executionHandler.ReceivedStart, Is.EqualTo(start));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(expectedCode));
        }

        private static LifecycleExecutionStartBinding CreateStart ()
        {
            var definition =
                new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
            var reference = LifecycleExecutionReferenceFactory.CreateRegistered(
                definition,
                Guid.NewGuid(),
                new ExecutionStatusLocator("refresh-adapter-test"));
            var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            return new LifecycleExecutionStartBinding(
                reference,
                new UnityProjectIdentity(
                    ProjectPathTestValues.RepositoryUnityProject,
                    ProjectFingerprintTestFactory.Create(
                        "refresh-adapter-test"),
                    "2023.2.22f1"),
                new LifecycleExecutionHostRegistration(
                    new ProcessIdentity(42, 123),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid()),
                new UnityEditorGenerationSnapshot(1, 2, 3, 4),
                DateTimeOffset.UtcNow.AddSeconds(30),
                startedAtUtc);
        }

        private static IpcRequestEnvelope CreateRequest (
            LifecycleExecutionStartBinding start)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(UnityIpcMethod.Refresh),
                IpcPayloadCodec.SerializeToElement(new IpcRefreshRequest(start)),
                "single",
                DateTimeOffset.UtcNow.AddSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private sealed class StubRefreshLifecycleExecutionHandler :
            IRefreshLifecycleExecutionHandler
        {
            private readonly RefreshLifecycleExecutionOutcome outcome;

            public StubRefreshLifecycleExecutionHandler (
                RefreshLifecycleExecutionOutcome outcome)
            {
                this.outcome = outcome;
            }

            public LifecycleExecutionStartBinding ReceivedStart { get; private set; }

            public ValueTask<RefreshLifecycleExecutionOutcome> ExecuteAsync (
                LifecycleExecutionStartBinding requestedStart)
            {
                ReceivedStart = requestedStart;
                return new ValueTask<RefreshLifecycleExecutionOutcome>(outcome);
            }
        }
    }
}
