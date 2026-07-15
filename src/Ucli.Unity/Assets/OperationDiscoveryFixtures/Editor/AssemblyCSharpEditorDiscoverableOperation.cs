using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.OperationDiscoveryFixtures
{
    [UcliOperation]
    internal sealed class AssemblyCSharpEditorDiscoverableOperation : UcliOperation<UcliEmptyArgs, UcliNoResult>
    {
        public const string OperationName = "ucli.tests.assembly-csharp-editor.discover";

        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
            operationName: OperationName,
            kind: UcliOperationKind.Query,
            describeContract: new UcliOperationDescribeContract(
                "Verifies that operations compiled into Assembly-CSharp-Editor are discoverable.",
                Array.Empty<UcliOperationInputContract>(),
                UcliOperationResultContract.NoResult("This fixture does not emit operation-specific result data."),
                new UcliOperationAssuranceContract(
                    sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                    touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                    planMode: UcliOperationPlanMode.ValidationOnly,
                    planSemantics: "Validate that the fixture operation can be registered.",
                    callSemantics: "Return success without reading or mutating Unity state.",
                    touchedContract: "Reports no touched resources.",
                    readPostconditionContract: "Does not stale read surfaces.",
                    failureSemantics: "Failure means the fixture operation did not complete.",
                    dangerousNotes: Array.Empty<string>()),
                codeContract: null));

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationPhaseStepResult.Success());
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationPhaseStepResult.Success());
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            UcliEmptyArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationPhaseStepResult.Success());
        }
    }
}
