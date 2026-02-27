using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecuteRequestDispatcherTests
    {
        [Test]
        [Category("Size.Small")]
        [TestCase(IpcExecuteCommandNames.Plan, PhaseExecutionCommand.Plan)]
        [TestCase(IpcExecuteCommandNames.Call, PhaseExecutionCommand.Call)]
        public async Task Dispatch_WhenCommandIsPlanOrCall_DelegatesToPhaseExecutor (
            string commandName,
            PhaseExecutionCommand expectedCommand)
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: new[]
                {
                    new OperationPhaseTrace("op-1", "ucli.resolve", OperationPhase.Plan, false, false, System.Array.Empty<OperationTouch>(), null),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(commandName);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(phaseExecutor.ReceivedCommand, Is.EqualTo(expectedCommand));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        [TestCase(IpcExecuteCommandNames.Resolve)]
        [TestCase(IpcExecuteCommandNames.Query)]
        [TestCase(IpcExecuteCommandNames.Refresh)]
        public async Task Dispatch_WhenCommandIsNotImplemented_ReturnsCommandNotImplementedError (string commandName)
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(commandName);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.CommandNotImplemented));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Dispatch_WhenNormalizationFails_ReturnsNormalizationError ()
        {
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(IpcErrorCodes.InvalidArgument, "invalid request", "op-1")));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(IpcExecuteCommandNames.Plan);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].OpId, Is.EqualTo("op-1"));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Size.Small")]
        public void Dispatch_WhenCancellationRequested_ThrowsOperationCanceledException ()
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(IpcExecuteCommandNames.Plan);
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.Dispatch(request, context, cancellationTokenSource.Token);
            });
        }

        private static IpcExecuteRequest CreateExecuteRequest (string commandName)
        {
            return new IpcExecuteRequest(
                commandName,
                JsonSerializer.SerializeToElement(new
                {
                    protocolVersion = 1,
                    requestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    ops = new[]
                    {
                        new
                        {
                            id = "op-1",
                            op = "ucli.resolve",
                            args = new { },
                        },
                    },
                }));
        }

        private static NormalizedExecuteRequest CreateNormalizedRequest ()
        {
            return new NormalizedExecuteRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Ops: new[]
                {
                    new NormalizedOperation(
                        Id: "op-1",
                        Op: "ucli.resolve",
                        Args: JsonSerializer.SerializeToElement(new { }),
                        As: null,
                        Expect: null),
                },
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes("{}"));
        }

        private sealed class StubExecuteRequestNormalizer : IExecuteRequestNormalizer
        {
            private readonly ExecuteRequestNormalizationResult normalizationResult;

            public StubExecuteRequestNormalizer (ExecuteRequestNormalizationResult normalizationResult)
            {
                this.normalizationResult = normalizationResult;
            }

            public ExecuteRequestNormalizationResult Normalize (
                IpcExecuteRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return normalizationResult;
            }
        }

        private sealed class SpyOperationPhaseExecutor : IOperationPhaseExecutor
        {
            private readonly PhaseExecutionTrace executionTrace;

            public SpyOperationPhaseExecutor (PhaseExecutionTrace executionTrace)
            {
                this.executionTrace = executionTrace;
            }

            public int CallCount { get; private set; }

            public PhaseExecutionCommand? ReceivedCommand { get; private set; }

            public Task<PhaseExecutionTrace> Execute (
                PhaseExecutionCommand command,
                NormalizedExecuteRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                ReceivedCommand = command;
                return Task.FromResult(executionTrace);
            }
        }
    }
}
