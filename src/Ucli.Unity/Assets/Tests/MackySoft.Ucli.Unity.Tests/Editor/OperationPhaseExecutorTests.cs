using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class OperationPhaseExecutorTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsPlan_ExecutesValidateAndPlanOnly () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                operationName: "ucli.resolve",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(applied: false, changed: true),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true));
            var executor = CreateExecutor(operation);
            var request = CreateRequest("op-1", "ucli.resolve");

            var trace = await executor.Execute(PhaseExecutionCommand.Plan, request).AsUniTask();

            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, operation.CalledPhases);
            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsCall_ExecutesValidatePlanAndCall () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                operationName: "ucli.resolve",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true));
            var executor = CreateExecutor(operation);
            var request = CreateRequest("op-1", "ucli.resolve");

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan, OperationPhase.Call }, operation.CalledPhases);
            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Call));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenValidateFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                operationName: "ucli.resolve",
                validateResult: OperationPhaseStepResult.Failed(new OperationFailure(IpcErrorCodes.InvalidArgument, "invalid", "op-1")),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var skippedOperation = new RecordingPhaseOperation(
                operationName: "ucli.scene.open",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(new InMemoryPhaseOperationRegistry(new IPhaseOperation[]
            {
                failingOperation,
                skippedOperation,
            }));
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.scene.open"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Validate));
            Assert.That(trace.OperationTraces[0].Failure, Is.Not.Null);
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Skipped));
            Assert.That(trace.OperationTraces[1].Applied, Is.False);
            Assert.That(trace.OperationTraces[1].Changed, Is.False);
            Assert.That(skippedOperation.CalledPhases.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenPlanFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                operationName: "ucli.resolve",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Failed(new OperationFailure(IpcErrorCodes.InvalidArgument, "plan failed", "op-1")),
                callResult: OperationPhaseStepResult.Success());
            var skippedOperation = new RecordingPhaseOperation(
                operationName: "ucli.scene.open",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(new InMemoryPhaseOperationRegistry(new IPhaseOperation[]
            {
                failingOperation,
                skippedOperation,
            }));
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.scene.open"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Skipped));
            Assert.That(skippedOperation.CalledPhases.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                operationName: "ucli.resolve",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Failed(new OperationFailure(IpcErrorCodes.InvalidArgument, "call failed", "op-1")));
            var skippedOperation = new RecordingPhaseOperation(
                operationName: "ucli.scene.open",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(new InMemoryPhaseOperationRegistry(new IPhaseOperation[]
            {
                failingOperation,
                skippedOperation,
            }));
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.scene.open"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Call));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Skipped));
            Assert.That(skippedOperation.CalledPhases.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCancellationRequested_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                operationName: "ucli.resolve",
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = CreateExecutor(operation);
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            var request = CreateRequest("op-1", "ucli.resolve");

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executor.Execute(PhaseExecutionCommand.Call, request, cancellationTokenSource.Token).AsUniTask();
            });
        });

        private static OperationPhaseExecutor CreateExecutor (IPhaseOperation operation)
        {
            return new OperationPhaseExecutor(new InMemoryPhaseOperationRegistry(new[] { operation }));
        }

        private static NormalizedExecuteRequest CreateRequest (
            string opId,
            string operationName)
        {
            return CreateRequest((opId, operationName));
        }

        private static NormalizedExecuteRequest CreateRequest (params (string OpId, string OperationName)[] operations)
        {
            var normalizedOperations = new List<NormalizedOperation>(operations.Length);
            for (var i = 0; i < operations.Length; i++)
            {
                var operation = operations[i];
                normalizedOperations.Add(new NormalizedOperation(
                    Id: operation.OpId,
                    Op: operation.OperationName,
                    Args: JsonSerializer.SerializeToElement(new { }),
                    As: null,
                    Expect: null));
            }

            return new NormalizedExecuteRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Ops: normalizedOperations,
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes("{}"));
        }

        private sealed class RecordingPhaseOperation : IPhaseOperation
        {
            private readonly OperationPhaseStepResult validateResult;
            private readonly OperationPhaseStepResult planResult;
            private readonly OperationPhaseStepResult callResult;

            public RecordingPhaseOperation (
                string operationName,
                OperationPhaseStepResult validateResult,
                OperationPhaseStepResult planResult,
                OperationPhaseStepResult callResult)
            {
                OperationName = operationName;
                this.validateResult = validateResult;
                this.planResult = planResult;
                this.callResult = callResult;
            }

            public string OperationName { get; }

            public List<OperationPhase> CalledPhases { get; } = new List<OperationPhase>();

            public Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Validate);
                return Task.FromResult(validateResult);
            }

            public Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Plan);
                return Task.FromResult(planResult);
            }

            public Task<OperationPhaseStepResult> Call (
                NormalizedOperation operation,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Call);
                return Task.FromResult(callResult);
            }
        }
    }
}
