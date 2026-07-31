using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PlayEnterUnityIpcMethodHandlerTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator HandleAsync_WithValidPayload_DelegatesTypedStartAndEncodesEnterErrorPayload () =>
            UniTask.ToCoroutine(async () =>
            {
                var start = CreateStart();
                var expectedCode = UcliCoreErrorCodes.InvalidArgument;
                var executionHandler =
                    new StubPlayEnterLifecycleExecutionHandler(
                        PlayEnterLifecycleExecutionOutcome.Failed(
                            expectedCode,
                            "Play Mode entry execution was not found.",
                            lifecycleExecutionRef: null,
                            ExecutionApplicationState.NotApplied,
                            result: null));
                var handler = new PlayEnterUnityIpcMethodHandler(
                    executionHandler,
                    NoOpDaemonLogger.Instance);

                var response =
                    await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        handler,
                        CreateRequest(start),
                        CancellationToken.None);

                Assert.That(executionHandler.ReceivedStart, Is.EqualTo(start));
                Assert.That(
                    response.Status,
                    Is.EqualTo(IpcResponseStatus.Error));
                Assert.That(
                    response.Errors.Single().Code,
                    Is.EqualTo(expectedCode));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        response.Payload,
                        out IpcPlayTransitionErrorResponse payload,
                        out _),
                    Is.True);
                Assert.That(payload.LifecycleExecutionRef, Is.Null);
                Assert.That(
                    payload.ApplicationState,
                    Is.EqualTo(ExecutionApplicationState.NotApplied));
                Assert.That(payload.Result, Is.Null);
            });

        private static LifecycleExecutionStartBinding CreateStart ()
        {
            var definition =
                new LifecycleExecutionDefinition(
                    LifecycleExecutionKind.PlayEnter);
            var reference = LifecycleExecutionReferenceFactory.CreateRegistered(
                definition,
                Guid.NewGuid(),
                new ExecutionStatusLocator("play-enter-adapter-test"));
            var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
            return new LifecycleExecutionStartBinding(
                reference,
                new UnityProjectIdentity(
                    ProjectPathTestValues.RepositoryUnityProject,
                    ProjectFingerprintTestFactory.Create(
                        "play-enter-adapter-test"),
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
                TextVocabulary.GetText(UnityIpcMethod.PlayEnter),
                IpcPayloadCodec.SerializeToElement(
                    new IpcPlayEnterRequest(start)),
                "single",
                DateTimeOffset.UtcNow.AddSeconds(30),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private sealed class StubPlayEnterLifecycleExecutionHandler :
            IPlayEnterLifecycleExecutionHandler
        {
            private readonly PlayEnterLifecycleExecutionOutcome outcome;

            public StubPlayEnterLifecycleExecutionHandler (
                PlayEnterLifecycleExecutionOutcome outcome)
            {
                this.outcome = outcome;
            }

            public LifecycleExecutionStartBinding ReceivedStart
            {
                get;
                private set;
            }

            public ValueTask<PlayEnterLifecycleExecutionOutcome> ExecuteAsync (
                LifecycleExecutionStartBinding requestedStart)
            {
                ReceivedStart = requestedStart;
                return new ValueTask<PlayEnterLifecycleExecutionOutcome>(
                    outcome);
            }
        }
    }
}
