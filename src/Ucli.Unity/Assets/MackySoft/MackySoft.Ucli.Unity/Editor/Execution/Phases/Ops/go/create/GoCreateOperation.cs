using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.go.create</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class GoCreateOperation : UcliOperation<GoCreateArgs, UcliNoResult>
    {
        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GoCreateArgs, UcliNoResult>(
            operationName: UcliPrimitiveOperationNames.GoCreate,
            kind: UcliOperationKind.Mutation,
            description: "Creates a GameObject in a scene or under an existing parent.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.SceneContentMutation, UcliOperationSideEffect.PrefabContentMutation },
                touchedKinds: new[] { UcliTouchedResourceKind.Scene, UcliTouchedResourceKind.Prefab },
                planMode: UcliOperationPlanMode.MayCreatePreviewState,
                planSemantics: "Validate the parent target and compute preview hierarchy changes without persisting project data.",
                callSemantics: "Create the GameObject in live Unity state and leave saving to explicit save operations.",
                touchedContract: "Reports the scene or prefab resource dirtied by the hierarchy mutation when the target can be resolved.",
                readPostconditionContract: "Scene, prefab, and hierarchy read surfaces covering touched resources may be stale until refreshed.",
                failureSemantics: "Failure before apply leaves no requested mutation; failure during apply may leave live Unity state partially changed.",
                dangerousNotes: new[] { "This operation can dirty scene or prefab hierarchy state without persisting it; callers must save or discard changes explicitly." }),
            exposure: UcliOperationExposure.EditLoweringOnly);

        /// <summary> Executes validate phase for <c>ucli.go.create</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            GoCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(operation, args, executionContext, allowTemporaryState: true, out _, out var failure))
            {
                return Task.FromResult(failure!);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        /// <summary> Executes plan phase for <c>ucli.go.create</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            GoCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(
                operation,
                args,
                executionContext,
                allowTemporaryState: true,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            if (!GoOperationUtilities.TryEnsurePlanResourceState(
                    validationState.Resource,
                    executionContext,
                    out var preparationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    preparationErrorMessage));
            }

            if (!TryValidateArguments(
                    operation,
                    args,
                    executionContext,
                    allowTemporaryState: true,
                    out validationState,
                    out failure))
            {
                return Task.FromResult(failure!);
            }

            if (!TryEnsureRequestLocalPlanDestination(validationState, executionContext, out var destinationErrorMessage))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    destinationErrorMessage));
            }

            var temporaryGameObject = GoOperationUtilities.CreateTemporaryGameObject(validationState.Name, executionContext);
            AttachGameObject(validationState, temporaryGameObject);
            GoOperationUtilities.MarkPlanResourceDirty(validationState.Resource, executionContext);
            executionContext.MarkRequestAttributedChange(validationState.Resource);
            if (operation.As != null)
            {
                executionContext.SetTemporaryAlias(operation.As, temporaryGameObject, validationState.Resource);
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: true,
                touched: new[]
                {
                    OperationResourceUtilities.CreateTouch(validationState.Resource),
                }));
        }

        /// <summary> Executes call phase for <c>ucli.go.create</c>. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            GoCreateArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateArguments(
                operation,
                args,
                executionContext,
                allowTemporaryState: false,
                out var validationState,
                out var failure))
            {
                return Task.FromResult(failure!);
            }

            var createdGameObject = new GameObject(validationState.Name);
            AttachGameObject(validationState, createdGameObject);

            executionContext.MarkRequestAttributedChange(validationState.Resource);
            StoreAliasIfNeeded(operation.As, executionContext, createdGameObject, validationState.Resource);
            return Task.FromResult(
                OperationPhaseStepResult.Success(
                    applied: true,
                    changed: true,
                    touched: new[]
                    {
                        OperationResourceUtilities.CreateTouch(validationState.Resource),
                    })
                .WithReadInvalidations(OperationReadInvalidationUtilities.CreateSceneTreeLiteForSceneResource(validationState.Resource)));
        }

        /// <summary> Validates arguments and resolves the destination resource and optional parent. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="executionContext"> The per-request execution context shared by all operations. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan aliases may satisfy parent resolution. </param>
        /// <param name="validationState"> The validated operation state when validation succeeds. </param>
        /// <param name="failure"> The failure result when validation fails. </param>
        /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryValidateArguments (
            NormalizedOperation operation,
            GoCreateArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ValidationState validationState,
            out OperationPhaseStepResult? failure)
        {
            validationState = default;
            failure = null;

            if (args.Parent == null)
            {
                if (!GoOperationUtilities.TryResolveScene(
                    args.Scene!.Value,
                    executionContext,
                    allowTemporaryState,
                    out var scene,
                    out var sceneErrorMessage))
                {
                    failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, sceneErrorMessage);
                    return false;
                }

                validationState = new ValidationState(
                    args.Name,
                    scene,
                    parent: null,
                    new OperationResource(UcliTouchedResourceKind.Scene, args.Scene.Value));
                return true;
            }

            if (!UnityObjectReferenceContractMapper.TryMap(
                    args.Parent,
                    "args.parent",
                    operation.AliasReferences,
                    out var parentReference,
                    out var parentErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parentErrorMessage);
                return false;
            }

            if (!GoOperationUtilities.TryResolveEditableGameObject(
                parentReference,
                executionContext,
                OperationObjectReferenceUtilities.GetReferenceResolutionPolicy(operation, allowTemporaryState),
                out var parentResolution,
                out parentErrorMessage))
            {
                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, parentErrorMessage);
                return false;
            }

            validationState = new ValidationState(
                args.Name,
                default,
                parentResolution.GameObject,
                parentResolution.Resource);
            return true;
        }

        /// <summary> Verifies that one plan-time create destination belongs to request-local mutable state. </summary>
        /// <param name="validationState"> The validated create destination. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="errorMessage"> The validation error message when destination does not belong to request-local plan state. </param>
        /// <returns> <see langword="true" /> when the create destination is request-local; otherwise <see langword="false" />. </returns>
        private static bool TryEnsureRequestLocalPlanDestination (
            ValidationState validationState,
            OperationExecutionContext executionContext,
            out string errorMessage)
        {
            if (validationState.Parent != null)
            {
                return GoOperationUtilities.TryEnsureRequestLocalPlanGameObject(
                    validationState.Parent,
                    validationState.Resource,
                    executionContext,
                    out errorMessage);
            }

            if (validationState.Resource.Kind != UcliTouchedResourceKind.Scene)
            {
                errorMessage = $"GameObject could not be projected into request-local plan state: {validationState.Resource.Path}.";
                return false;
            }

            if (!executionContext.TryGetTemporaryScene(validationState.Resource.Path, out var temporaryScene)
                || validationState.Scene != temporaryScene)
            {
                errorMessage = $"GameObject could not be projected into request-local plan state: {validationState.Resource.Path}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Moves one created GameObject into the validated destination scene or parent. </summary>
        /// <param name="validationState"> The validated operation state. </param>
        /// <param name="createdGameObject"> The created GameObject. </param>
        private static void AttachGameObject (
            ValidationState validationState,
            GameObject createdGameObject)
        {
            if (validationState.Parent != null)
            {
                SceneManager.MoveGameObjectToScene(createdGameObject, validationState.Parent.scene);
                createdGameObject.transform.SetParent(validationState.Parent.transform, worldPositionStays: false);
                return;
            }

            SceneManager.MoveGameObjectToScene(createdGameObject, validationState.Scene);
        }

        /// <summary> Stores one alias for the created GameObject when the request specifies <c>as</c>. </summary>
        /// <param name="alias"> The optional alias name. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="createdGameObject"> The created GameObject. </param>
        /// <param name="resource"> The owner resource for the created GameObject. </param>
        private static void StoreAliasIfNeeded (
            RequestLocalAliasIdentity? alias,
            OperationExecutionContext executionContext,
            GameObject createdGameObject,
            OperationResource resource)
        {
            if (alias == null)
            {
                return;
            }

            executionContext.SetTemporaryAlias(alias, createdGameObject, resource);
            if (UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(createdGameObject, out var globalObjectId))
            {
                executionContext.AliasStore.Set(alias, globalObjectId);
            }
        }

        private readonly struct ValidationState
        {
            public ValidationState (
                string name,
                Scene scene,
                GameObject? parent,
                OperationResource resource)
            {
                Name = name;
                Scene = scene;
                Parent = parent;
                Resource = resource;
            }

            public string Name { get; }

            public Scene Scene { get; }

            public GameObject? Parent { get; }

            public OperationResource Resource { get; }
        }
    }
}
