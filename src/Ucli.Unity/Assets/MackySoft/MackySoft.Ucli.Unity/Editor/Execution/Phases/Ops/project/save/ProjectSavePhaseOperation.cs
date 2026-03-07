using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.project.save</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class ProjectSavePhaseOperation : IUcliOperation
    {
        public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
            operationName: "ucli.project.save",
            kind: UcliOperationKind.Mutation,
            policy: OperationPolicy.Advanced);

        /// <summary> Executes validate phase for <c>ucli.project.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ProjectOperationUtilities.TryValidateEmptyArguments(operation.Args, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.project.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ProjectOperationUtilities.TryValidateEmptyArguments(operation.Args, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes call phase for <c>ucli.project.save</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        public Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ProjectOperationUtilities.TryValidateEmptyArguments(operation.Args, out var errorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, errorMessage));
            }

            var projectRoot = ProjectOperationUtilities.ResolveProjectRootPath();
            var beforeSnapshot = ProjectOperationUtilities.CaptureProjectSettingsSnapshot(projectRoot);
            var scopeId = ProjectOperationCallbackRegistry.BeginSaveCapture();
            IReadOnlyList<string> callbackPaths;
            bool executed;
            try
            {
                executed = EditorApplication.ExecuteMenuItem("File/Save Project");
            }
            finally
            {
                callbackPaths = ProjectOperationCallbackRegistry.EndSaveCapture(scopeId);
            }

            if (!executed)
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    "Project could not be saved."));
            }

            var afterSnapshot = ProjectOperationUtilities.CaptureProjectSettingsSnapshot(projectRoot);
            var changedProjectSettingsPaths = ProjectOperationUtilities.GetChangedProjectSettingsPaths(beforeSnapshot, afterSnapshot);
            var touched = ProjectOperationUtilities.CreateTouchedResources(callbackPaths, changedProjectSettingsPaths);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: touched.Count != 0,
                touched: touched));
        }
    }
}
