using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary>
    /// <para> Compiles validated public request steps into primitive-only execute-request models. </para>
    /// <para> <c>kind:"edit"</c> steps are expanded into concrete primitive chains, while <c>kind:"op"</c> steps are forwarded unchanged. </para>
    /// </summary>
    internal sealed class ExecuteRequestCompiler
    {
        private const string EditOperationName = "edit";

        /// <summary>
        /// Validates one parsed request step list and preserves the validated source steps for runtime compilation.
        /// </summary>
        /// <param name="requestContract"> The validated public request contract. </param>
        /// <param name="sourceSteps"> The validated source steps in source order when validation succeeds. </param>
        /// <param name="error"> The structured normalization error when compilation fails. </param>
        /// <returns> <see langword="true" /> when every source step is valid for runtime compilation; otherwise <see langword="false" />. </returns>
        public bool TryPrepareSourceSteps (
            IpcRequestContract requestContract,
            out IReadOnlyList<IpcRequestContractStep> sourceSteps,
            out ExecuteRequestNormalizationError error)
        {
            sourceSteps = Array.Empty<IpcRequestContractStep>();
            error = default!;

            if (requestContract.Steps == null)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'steps' is required.",
                    opId: null);
                return false;
            }

            var preparedSteps = new List<IpcRequestContractStep>(requestContract.Steps.Count);
            foreach (var step in requestContract.Steps)
            {
                if (step == null || step.Id == null || step.Kind == null)
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Request step is incomplete.",
                        opId: step?.Id);
                    return false;
                }

                switch (step.Kind)
                {
                    case IpcRequestStepKind.Op:
                        if (!TryValidateOpStep(step, out error))
                        {
                            return false;
                        }

                        break;

                    case IpcRequestStepKind.Edit:
                        if (!TryValidateEditStep(step, out error))
                        {
                            return false;
                        }

                        break;

                    default:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            message: $"Step '{step.Id}' has unsupported kind.",
                            opId: step.Id);
                        return false;
                }

                preparedSteps.Add(step);
            }

            sourceSteps = preparedSteps;
            error = default!;
            return true;
        }

        /// <summary>
        /// Compiles one validated source step against the current request-local execution state.
        /// </summary>
        /// <param name="step"> The validated public source step. </param>
        /// <param name="executionContext"> The current request execution context used for dynamic selection resolution. </param>
        /// <param name="compiledStep"> The compiled public step metadata when compilation succeeds. </param>
        /// <param name="operations"> The compiled primitive operations in execution order when compilation succeeds. </param>
        /// <param name="error"> The structured normalization error when compilation fails. </param>
        /// <returns> <see langword="true" /> when the source step can be compiled for the current execution state; otherwise <see langword="false" />. </returns>
        public bool TryCompileExecutionStep (
            IpcRequestContractStep step,
            OperationExecutionContext executionContext,
            out NormalizedRequestStep compiledStep,
            out IReadOnlyList<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            compiledStep = default!;
            operations = Array.Empty<NormalizedOperation>();

            if (step.Kind == IpcRequestStepKind.Op)
            {
                return TryCompileOpStep(step, out compiledStep, out operations, out error);
            }

            if (step.Kind == IpcRequestStepKind.Edit)
            {
                return TryCompileEditStep(step, executionContext, out compiledStep, out operations, out error);
            }

            error = ExecuteRequestNormalizationError.InvalidArgument(
                message: $"Step '{step.Id}' has unsupported kind.",
                opId: step.Id);
            return false;
        }

        private static bool TryValidateOpStep (
            IpcRequestContractStep step,
            out ExecuteRequestNormalizationError error)
        {
            if (step.OperationName == null)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Step operation name is required.",
                    opId: step.Id);
                return false;
            }

            if (!step.Element.TryGetProperty("args", out var argsElement)
                || argsElement.ValueKind != JsonValueKind.Object)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: $"Step '{step.Id}' property 'args' must be an object.",
                    opId: step.Id);
                return false;
            }

            error = default!;
            return true;
        }

        private static bool TryCompileOpStep (
            IpcRequestContractStep step,
            out NormalizedRequestStep compiledStep,
            out IReadOnlyList<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            compiledStep = default!;
            operations = Array.Empty<NormalizedOperation>();
            if (!TryValidateOpStep(step, out error))
            {
                return false;
            }

            operations = new[]
            {
                new NormalizedOperation(
                    Id: step.Id!,
                    Op: step.OperationName!,
                    Args: step.Element.GetProperty("args").Clone(),
                    As: null,
                    Expect: null,
                    AllowRequestLocalAliases: false),
            };
            compiledStep = new NormalizedRequestStep(
                Id: step.Id!,
                Kind: IpcRequestStepKind.Op,
                OperationName: step.OperationName!,
                PrimitiveCount: operations.Count);
            error = default!;
            return true;
        }

        private bool TryValidateEditStep (
            IpcRequestContractStep step,
            out ExecuteRequestNormalizationError error)
        {
            if (!IpcEditStepContractReader.TryRead(step.Element, out var editStep, out var editErrorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: editErrorMessage,
                    opId: step.Id);
                return false;
            }

            if (editStep.Selection.Kind == IpcEditStepContract.SelectionKind.Direct
                && !TryBuildDirectSelectionTarget(editStep, out _, out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(errorMessage, step.Id);
                return false;
            }

            error = default!;
            return true;
        }

        private bool TryCompileEditStep (
            IpcRequestContractStep step,
            OperationExecutionContext executionContext,
            out NormalizedRequestStep compiledStep,
            out IReadOnlyList<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            compiledStep = default!;
            operations = Array.Empty<NormalizedOperation>();
            if (!IpcEditStepContractReader.TryRead(step.Element, out var editStep, out var editErrorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: editErrorMessage,
                    opId: step.Id);
                return false;
            }

            var stepOperations = new List<NormalizedOperation>(editStep.Actions.Count + 4);
            var shouldReleaseImplicitExecutionContextOnNoTargets =
                ShouldReleaseImplicitExecutionContextOnNoTargets(editStep, executionContext);
            if (!TryEnsureImplicitExecutionContext(editStep, executionContext, out error))
            {
                return false;
            }

            if (!TryResolveSelection(editStep, executionContext, out var selectedTargets, out error))
            {
                return false;
            }

            if (selectedTargets.Count > 0)
            {
                if (!TryValidateLiveEditContextAvailability(editStep, executionContext, out error))
                {
                    return false;
                }

                for (var branchIndex = 0; branchIndex < selectedTargets.Count; branchIndex++)
                {
                    if (!TryCompileBranch(
                            editStep,
                            branchIndex,
                            selectedTargets[branchIndex],
                            stepOperations,
                            out error))
                    {
                        return false;
                    }
                }

            }
            else if (shouldReleaseImplicitExecutionContextOnNoTargets)
            {
                ReleaseImplicitExecutionContext(editStep, executionContext);
            }

            if (!TryValidateCommitContextAvailability(editStep, executionContext, out error))
            {
                return false;
            }

            if (!TryAddCommitOperation(stepOperations, editStep, out error))
            {
                return false;
            }

            compiledStep = new NormalizedRequestStep(
                Id: editStep.Id,
                Kind: IpcRequestStepKind.Edit,
                OperationName: EditOperationName,
                PrimitiveCount: stepOperations.Count);
            operations = stepOperations;
            error = default!;
            return true;
        }

        private static bool TryValidateLiveEditContextAvailability (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            out ExecuteRequestNormalizationError error)
        {
            error = default!;
            if (!IpcEditStepLoweringRules.RequiresLiveEditableContext(step))
            {
                return true;
            }

            switch (step.Context.Kind)
            {
                case IpcEditStepContract.ContextKind.Scene:
                    if (SceneOperationUtilities.TryGetLoadedScene(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLiveSceneOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step '{step.Id}' mutates scene context '{step.Context.Path}', but the scene is not loaded. Add 'ucli.scene.open' before this step.",
                        step.Id);
                    return false;

                case IpcEditStepContract.ContextKind.Prefab:
                    if (PrefabOperationUtilities.TryGetOpenedPrefabStage(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLivePrefabOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step '{step.Id}' mutates prefab context '{step.Context.Path}', but the prefab is not opened. Add 'ucli.prefab.open' before this step.",
                        step.Id);
                    return false;

                default:
                    return true;
            }
        }

        private static bool ShouldReleaseImplicitExecutionContextOnNoTargets (
            IpcEditStepContract step,
            OperationExecutionContext executionContext)
        {
            switch (step.Context.Kind)
            {
                case IpcEditStepContract.ContextKind.Scene:
                    return !executionContext.TryGetTemporaryScene(step.Context.Path!, out _);

                case IpcEditStepContract.ContextKind.Prefab:
                    return !executionContext.TryGetTemporaryPrefabContentsRoot(step.Context.Path!, out _);

                default:
                    return false;
            }
        }

        private static void ReleaseImplicitExecutionContext (
            IpcEditStepContract step,
            OperationExecutionContext executionContext)
        {
            switch (step.Context.Kind)
            {
                case IpcEditStepContract.ContextKind.Scene:
                    executionContext.ReleaseTemporaryScene(step.Context.Path!);
                    break;

                case IpcEditStepContract.ContextKind.Prefab:
                    executionContext.ReleaseTemporaryPrefabExecutionSession(step.Context.Path!);
                    break;
            }
        }

        private static bool TryValidateCommitContextAvailability (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            out ExecuteRequestNormalizationError error)
        {
            error = default!;
            var operationName = IpcEditStepLoweringRules.GetCommitOperationName(step.Context.Kind, step.Commit);
            if (operationName == null)
            {
                return true;
            }

            switch (operationName)
            {
                case UcliPrimitiveOperationNames.SceneSave:
                    if (SceneOperationUtilities.TryGetLoadedScene(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLiveSceneOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step '{step.Id}' saves scene context '{step.Context.Path}', but the scene is not loaded. Add 'ucli.scene.open' before this step.",
                        step.Id);
                    return false;

                case UcliPrimitiveOperationNames.PrefabSave:
                    if (PrefabOperationUtilities.TryGetOpenedPrefabStage(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLivePrefabOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step '{step.Id}' saves prefab context '{step.Context.Path}', but the prefab is not opened. Add 'ucli.prefab.open' before this step.",
                        step.Id);
                    return false;

                default:
                    return true;
            }
        }

        private static bool TryEnsureImplicitExecutionContext (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            out ExecuteRequestNormalizationError error)
        {
            error = default!;
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            switch (step.Context.Kind)
            {
                case IpcEditStepContract.ContextKind.Scene:
                    if (executionContext.TryEnsureSceneExecutionSession(step.Context.Path!, out var sceneErrorMessage))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(sceneErrorMessage, step.Id);
                    return false;

                case IpcEditStepContract.ContextKind.Prefab:
                    if (executionContext.TryEnsurePrefabExecutionSession(step.Context.Path!, out var prefabErrorMessage))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(prefabErrorMessage, step.Id);
                    return false;

                default:
                    return true;
            }
        }

        private bool TryResolveSelection (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            out List<SelectionTarget> selectedTargets,
            out ExecuteRequestNormalizationError error)
        {
            selectedTargets = new List<SelectionTarget>();
            if (step.Selection.Kind == IpcEditStepContract.SelectionKind.Direct)
            {
                if (!TryResolveDirectSelectionTargets(step, executionContext, selectedTargets, out error))
                {
                    return false;
                }
            }
            else
            {
                if (!SceneQuerySelectionEngine.TryResolveForEditRuntime(
                    step,
                    executionContext,
                    out var matches,
                    out var errorMessage))
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(errorMessage, step.Id);
                    return false;
                }

                selectedTargets = new List<SelectionTarget>(matches.Count);
                for (var i = 0; i < matches.Count; i++)
                {
                    selectedTargets.Add(CreateSceneSelectionTarget(step.Context.Path!, matches[i]));
                }
            }

            if (!TryApplyCardinality(step.Id, step.Selection.Cardinality, selectedTargets, out error))
            {
                return false;
            }

            if (!TryValidateSelectedTargetActions(step, selectedTargets.Count, out error))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveDirectSelectionTargets (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            List<SelectionTarget> selectedTargets,
            out ExecuteRequestNormalizationError error)
        {
            if (!TryBuildDirectSelectionTarget(step, out var target, out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(errorMessage, step.Id);
                return false;
            }

            if (!TryResolveDirectSelectionTargetPresence(target, executionContext, out var hasMatch, out errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(errorMessage, step.Id);
                return false;
            }

            if (hasMatch)
            {
                selectedTargets.Add(target);
            }

            error = default!;
            return true;
        }

        private static bool TryResolveDirectSelectionTargetPresence (
            SelectionTarget target,
            OperationExecutionContext executionContext,
            out bool hasMatch,
            out string errorMessage)
        {
            hasMatch = false;
            if (!UnityObjectReferenceCodec.TryParse(target.Reference, "select", out var reference, out errorMessage))
            {
                return false;
            }

            if (UnityObjectReferenceResolver.TryResolve(reference, executionContext, allowTemporaryState: true, out _, out errorMessage))
            {
                hasMatch = true;
                errorMessage = string.Empty;
                return true;
            }

            if (IsDirectSelectionNoMatch(errorMessage))
            {
                errorMessage = string.Empty;
                return true;
            }

            return false;
        }

        private static bool IsDirectSelectionNoMatch (string errorMessage)
        {
            return errorMessage.StartsWith("Hierarchy path was not found", StringComparison.Ordinal)
                   || errorMessage.Contains("' was not found at '", StringComparison.Ordinal)
                   || errorMessage.StartsWith("Asset path could not be resolved to a main asset:", StringComparison.Ordinal);
        }

        private static bool TryApplyCardinality (
            string stepId,
            IpcEditStepContract.CardinalityKind cardinality,
            List<SelectionTarget> selectedTargets,
            out ExecuteRequestNormalizationError error)
        {
            switch (cardinality)
            {
                case IpcEditStepContract.CardinalityKind.One:
                    if (selectedTargets.Count != 1)
                    {
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Edit step '{stepId}' cardinality 'one' requires exactly one target.",
                            stepId);
                        return false;
                    }

                    break;

                case IpcEditStepContract.CardinalityKind.First:
                    if (selectedTargets.Count == 0)
                    {
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Edit step '{stepId}' cardinality 'first' requires at least one target.",
                            stepId);
                        return false;
                    }

                    selectedTargets.RemoveRange(1, selectedTargets.Count - 1);
                    break;

                case IpcEditStepContract.CardinalityKind.AtMostOne:
                    if (selectedTargets.Count > 1)
                    {
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            $"Edit step '{stepId}' cardinality 'atMostOne' requires zero or one target.",
                            stepId);
                        return false;
                    }

                    break;
            }

            error = default!;
            return true;
        }

        private static bool TryValidateSelectedTargetActions (
            IpcEditStepContract step,
            int selectedTargetCount,
            out ExecuteRequestNormalizationError error)
        {
            if (selectedTargetCount <= 1)
            {
                error = default!;
                return true;
            }

            for (var actionIndex = 0; actionIndex < step.Actions.Count; actionIndex++)
            {
                var actionKind = step.Actions[actionIndex].Kind;
                if (actionKind != IpcEditStepContract.ActionKind.CreateAsset
                    && actionKind != IpcEditStepContract.ActionKind.CreatePrefab)
                {
                    continue;
                }

                error = ExecuteRequestNormalizationError.InvalidArgument(
                    $"Edit step '{step.Id}' action '{ToActionLiteral(actionKind)}' requires the selection to resolve to at most one target.",
                    step.Id);
                return false;
            }

            error = default!;
            return true;
        }

        private static bool TryCompileBranch (
            IpcEditStepContract step,
            int branchIndex,
            SelectionTarget branchTarget,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            var aliases = new Dictionary<string, SelectionTarget>(StringComparer.Ordinal);
            for (var actionIndex = 0; actionIndex < step.Actions.Count; actionIndex++)
            {
                var action = step.Actions[actionIndex];
                if (!TryCompileAction(
                    step,
                    branchIndex,
                    branchTarget,
                    aliases,
                    action,
                    operations,
                    out error))
                {
                    return false;
                }
            }

            error = default!;
            return true;
        }

        private static bool TryCompileAction (
            IpcEditStepContract step,
            int branchIndex,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            switch (action.Kind)
            {
                case IpcEditStepContract.ActionKind.Set:
                    return TryCompileSetAction(step, branchTarget, aliases, action, operations, out error);

                case IpcEditStepContract.ActionKind.EnsureComponent:
                    return TryCompileEnsureComponentAction(step, branchIndex, branchTarget, aliases, action, operations, out error);

                case IpcEditStepContract.ActionKind.CreateObject:
                    return TryCompileCreateObjectAction(step, branchIndex, branchTarget, aliases, action, operations, out error);

                case IpcEditStepContract.ActionKind.CreateAsset:
                    return TryCompileCreateAssetAction(step, action, operations, out error);

                case IpcEditStepContract.ActionKind.CreatePrefab:
                    return TryCompileCreatePrefabAction(step, branchTarget, aliases, action, operations, out error);

                case IpcEditStepContract.ActionKind.Delete:
                    return TryCompileDeleteAction(step, branchTarget, aliases, action, operations, out error);

                case IpcEditStepContract.ActionKind.Reparent:
                    return TryCompileReparentAction(step, branchTarget, aliases, action, operations, out error);

                default:
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Unsupported edit action kind '{action.Kind}'.",
                        step.Id);
                    return false;
            }
        }

        private static bool TryCompileSetAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            if (!TryResolveTarget(step, branchTarget, aliases, action.Target, out var target, out error))
            {
                return false;
            }

            if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                    step.Context.Kind,
                    action.Kind,
                    target.Kind,
                    parentTargetKind: null,
                    out var operationName,
                    out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    step.Id);
                return false;
            }

            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: CreateSetArgs(target.Reference, action.Values),
                As: null,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));
            error = default!;
            return true;
        }

        private static bool TryCompileEnsureComponentAction (
            IpcEditStepContract step,
            int branchIndex,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            if (!TryResolveTarget(step, branchTarget, aliases, action.Target, out var target, out error))
            {
                return false;
            }

            if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                    step.Context.Kind,
                    action.Kind,
                    target.Kind,
                    parentTargetKind: null,
                    out var operationName,
                    out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    step.Id);
                return false;
            }

            var internalAlias = CreateInternalAlias(step.Id, branchIndex, action.Alias);
            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: CreateEnsureComponentArgs(target.Reference, action.Type!),
                As: internalAlias,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));

            if (action.Alias != null)
            {
                aliases[action.Alias] = new SelectionTarget(IpcEditTargetKind.Component, CreateAliasReference(internalAlias!));
            }

            error = default!;
            return true;
        }

        private static bool TryCompileCreateObjectAction (
            IpcEditStepContract step,
            int branchIndex,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                    step.Context.Kind,
                    action.Kind,
                    branchTarget.Kind,
                    parentTargetKind: null,
                    out var operationName,
                    out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    step.Id);
                return false;
            }

            var internalAlias = CreateInternalAlias(step.Id, branchIndex, action.Alias);
            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: CreateGoCreateArgs(action.Name!, branchTarget.Reference),
                As: internalAlias,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));

            if (action.Alias != null)
            {
                aliases[action.Alias] = new SelectionTarget(IpcEditTargetKind.GameObject, CreateAliasReference(internalAlias!));
            }

            error = default!;
            return true;
        }

        private static bool TryCompileCreateAssetAction (
            IpcEditStepContract step,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                    step.Context.Kind,
                    action.Kind,
                    IpcEditTargetKind.Asset,
                    parentTargetKind: null,
                    out var operationName,
                    out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    step.Id);
                return false;
            }

            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: CreateAssetCreateArgs(action.Type!, action.Path!),
                As: null,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));
            error = default!;
            return true;
        }

        private static bool TryCompileCreatePrefabAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            if (!TryResolveTarget(step, branchTarget, aliases, action.Target, out var target, out error))
            {
                return false;
            }

            if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                    step.Context.Kind,
                    action.Kind,
                    target.Kind,
                    parentTargetKind: null,
                    out var operationName,
                    out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    step.Id);
                return false;
            }

            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: CreatePrefabCreateArgs(target.Reference, action.Path!),
                As: null,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));
            error = default!;
            return true;
        }

        private static bool TryCompileDeleteAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            if (!TryResolveTarget(step, branchTarget, aliases, action.Target, out var target, out error))
            {
                return false;
            }

            if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                    step.Context.Kind,
                    action.Kind,
                    target.Kind,
                    parentTargetKind: null,
                    out var operationName,
                    out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    step.Id);
                return false;
            }

            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: CreateDeleteArgs(target.Reference),
                As: null,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));
            error = default!;
            return true;
        }

        private static bool TryCompileReparentAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            if (!TryResolveTarget(step, branchTarget, aliases, action.Target, out var target, out error))
            {
                return false;
            }

            if (!TryResolveParentTarget(step, branchTarget, aliases, action.Parent!, out var parent, out error))
            {
                return false;
            }

            if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                    step.Context.Kind,
                    action.Kind,
                    target.Kind,
                    parent.Kind,
                    out var operationName,
                    out var errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    step.Id);
                return false;
            }

            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: CreateReparentArgs(target.Reference, parent.Reference),
                As: null,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));
            error = default!;
            return true;
        }

        private static bool TryAddCommitOperation (
            ICollection<NormalizedOperation> operations,
            IpcEditStepContract step,
            out ExecuteRequestNormalizationError error)
        {
            var operationName = IpcEditStepLoweringRules.GetCommitOperationName(step.Context.Kind, step.Commit);
            if (operationName == null)
            {
                error = default!;
                return true;
            }

            var args = operationName == UcliPrimitiveOperationNames.ProjectSave
                ? CreateEmptyArgs()
                : CreatePathArgs(step.Context.Path!);
            operations.Add(new NormalizedOperation(
                Id: step.Id,
                Op: operationName,
                Args: args,
                As: null,
                Expect: null,
                InternalExecutionKey: CreateInternalExecutionKey(step.Id, operations.Count)));
            error = default!;
            return true;
        }

        private static bool TryBuildDirectSelectionTarget (
            IpcEditStepContract step,
            out SelectionTarget target,
            out string errorMessage)
        {
            switch (step.Context.Kind)
            {
                case IpcEditStepContract.ContextKind.Scene:
                    target = new SelectionTarget(
                        IpcEditStepLoweringRules.DetermineDirectSelectionTargetKind(step.Context.Kind, step.Selection.ComponentType),
                        CreateSceneSelector(step.Context.Path!, step.Selection.GameObjectPath!, step.Selection.ComponentType));
                    errorMessage = string.Empty;
                    return true;
                case IpcEditStepContract.ContextKind.Prefab:
                    target = new SelectionTarget(
                        IpcEditStepLoweringRules.DetermineDirectSelectionTargetKind(step.Context.Kind, step.Selection.ComponentType),
                        CreatePrefabSelector(step.Context.Path!, step.Selection.GameObjectPath!, step.Selection.ComponentType));
                    errorMessage = string.Empty;
                    return true;
                case IpcEditStepContract.ContextKind.Asset:
                    target = new SelectionTarget(IpcEditTargetKind.Asset, CreateAssetPathSelector(step.Context.Path!));
                    errorMessage = string.Empty;
                    return true;
                case IpcEditStepContract.ContextKind.Project:
                    target = new SelectionTarget(IpcEditTargetKind.Asset, CreateProjectAssetPathSelector(step.Selection.ProjectAssetPath!));
                    errorMessage = string.Empty;
                    return true;
                default:
                    target = default;
                    errorMessage = "Edit step direct selection context is unsupported.";
                    return false;
            }
        }

        /// <summary>
        /// Converts one deterministic scene-query match into the primitive selector payload for one compiled edit branch.
        /// </summary>
        /// <param name="scenePath"> The owning scene asset path for the enclosing edit step. </param>
        /// <param name="match"> The scene-query match selected for one branch. </param>
        /// <returns> One compiled selection target backed by a scene selector payload. </returns>
        private static SelectionTarget CreateSceneSelectionTarget (
            string scenePath,
            SceneQuerySelectionEngine.QueryMatch match)
        {
            return new SelectionTarget(
                match.TargetKind,
                CreateSceneSelector(
                    scenePath,
                    match.HierarchyPath,
                    match.ComponentType));
        }

        private static bool TryResolveTarget (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            string? targetLiteral,
            out SelectionTarget target,
            out ExecuteRequestNormalizationError error)
        {
            if (targetLiteral == null)
            {
                target = branchTarget;
                error = default!;
                return true;
            }

            if (!TryResolveAlias(aliases, targetLiteral, out target))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    $"Edit action target binding was not found: {targetLiteral}.",
                    step.Id);
                return false;
            }

            error = default!;
            return true;
        }

        private static bool TryResolveParentTarget (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            string parentLiteral,
            out SelectionTarget target,
            out ExecuteRequestNormalizationError error)
        {
            if (IsAliasReference(parentLiteral))
            {
                if (!TryResolveAlias(aliases, parentLiteral, out target))
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit action parent binding was not found: {parentLiteral}.",
                        step.Id);
                    return false;
                }

                error = default!;
                return true;
            }

            if (!IpcEditStepLoweringRules.SupportsDirectHierarchyParentLiteral(step.Context.Kind))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    "Edit action 'reparent' can use direct parent paths only in scene or prefab context.",
                    step.Id);
                target = default;
                return false;
            }

            if (step.Context.Kind == IpcEditStepContract.ContextKind.Scene)
            {
                target = new SelectionTarget(IpcEditTargetKind.GameObject, CreateSceneSelector(step.Context.Path!, parentLiteral, null));
                error = default!;
                return true;
            }

            target = new SelectionTarget(IpcEditTargetKind.GameObject, CreatePrefabSelector(step.Context.Path!, parentLiteral, null));
            error = default!;
            return true;
        }

        private static bool TryResolveAlias (
            IDictionary<string, SelectionTarget> aliases,
            string aliasReference,
            out SelectionTarget target)
        {
            target = default;
            if (!IsAliasReference(aliasReference))
            {
                return false;
            }

            return aliases.TryGetValue(aliasReference[1..], out target);
        }

        private static bool IsAliasReference (string literal)
        {
            return !string.IsNullOrWhiteSpace(literal)
                   && literal.Length > 1
                   && literal[0] == '$';
        }

        private static string? CreateInternalAlias (
            string stepId,
            int branchIndex,
            string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return null;
            }

            return $"__edit:{stepId}:{branchIndex}:{alias}";
        }

        private static string CreateInternalExecutionKey (
            string stepId,
            int primitiveIndex)
        {
            return $"{stepId}#p{primitiveIndex}";
        }

        private static JsonElement CreatePathArgs (string path)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("path", path);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateEmptyArgs ()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateAliasReference (string alias)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("var", alias);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateAssetPathSelector (string assetPath)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("assetPath", assetPath);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateProjectAssetPathSelector (string assetPath)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("projectAssetPath", assetPath);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        /// <summary>
        /// Creates one scene-scoped selector reference for primitive operation arguments.
        /// </summary>
        /// <param name="scenePath"> The owning scene asset path. </param>
        /// <param name="hierarchyPath"> The hierarchy path inside the scene. </param>
        /// <param name="componentType"> The optional component type selector. </param>
        /// <returns> One cloned selector object that targets the requested scene object or component. </returns>
        internal static JsonElement CreateSceneSelector (
            string scenePath,
            string hierarchyPath,
            string? componentType)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("scene", scenePath);
                writer.WriteString("hierarchyPath", hierarchyPath);
                if (!string.IsNullOrWhiteSpace(componentType))
                {
                    writer.WriteString("componentType", componentType);
                }

                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        /// <summary>
        /// Creates one prefab-scoped selector reference for primitive operation arguments.
        /// </summary>
        /// <param name="prefabPath"> The owning prefab asset path. </param>
        /// <param name="hierarchyPath"> The hierarchy path inside the prefab root. </param>
        /// <param name="componentType"> The optional component type selector. </param>
        /// <returns> One cloned selector object that targets the requested prefab object or component. </returns>
        internal static JsonElement CreatePrefabSelector (
            string prefabPath,
            string hierarchyPath,
            string? componentType)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("prefab", prefabPath);
                writer.WriteString("hierarchyPath", hierarchyPath);
                if (!string.IsNullOrWhiteSpace(componentType))
                {
                    writer.WriteString("componentType", componentType);
                }

                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateSetArgs (
            JsonElement targetReference,
            JsonElement values)
        {
            var assignments = values.EnumerateObject()
                .OrderBy(static property => property.Name, StringComparer.Ordinal)
                .Select(static property => new SetAssignment(property.Name, property.Value.Clone()))
                .ToArray();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("sets");
                writer.WriteStartArray();
                for (var i = 0; i < assignments.Length; i++)
                {
                    writer.WriteStartObject();
                    writer.WriteString("path", assignments[i].Path);
                    writer.WritePropertyName("value");
                    assignments[i].Value.WriteTo(writer);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WritePropertyName("target");
                targetReference.WriteTo(writer);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateEnsureComponentArgs (
            JsonElement targetReference,
            string typeId)
        {
            return CreateTargetTypeArgs(targetReference, typeId);
        }

        private static JsonElement CreatePrefabCreateArgs (
            JsonElement targetReference,
            string path)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("path", path);
                writer.WritePropertyName("target");
                targetReference.WriteTo(writer);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateGoCreateArgs (
            string name,
            JsonElement parentReference)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("name", name);
                writer.WritePropertyName("parent");
                parentReference.WriteTo(writer);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateAssetCreateArgs (
            string typeId,
            string path)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("path", path);
                writer.WriteString("type", typeId);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateDeleteArgs (JsonElement targetReference)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("target");
                targetReference.WriteTo(writer);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateReparentArgs (
            JsonElement targetReference,
            JsonElement parentReference)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("parent");
                parentReference.WriteTo(writer);
                writer.WritePropertyName("target");
                targetReference.WriteTo(writer);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateTargetTypeArgs (
            JsonElement targetReference,
            string typeId)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("target");
                targetReference.WriteTo(writer);
                writer.WriteString("type", typeId);
                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement ParseElement (MemoryStream stream)
        {
            stream.Position = 0;
            using var document = JsonDocument.Parse(stream.ToArray());
            return document.RootElement.Clone();
        }

        /// <summary>
        /// Represents one compiled edit-branch target together with its primitive reference payload.
        /// </summary>
        internal readonly struct SelectionTarget
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SelectionTarget" /> struct.
            /// </summary>
            /// <param name="kind"> The target category selected for the branch. </param>
            /// <param name="reference"> The cloned primitive reference payload that identifies the target. </param>
            public SelectionTarget (
                IpcEditTargetKind kind,
                JsonElement reference)
            {
                Kind = kind;
                Reference = reference;
            }

            /// <summary>
            /// Gets the primitive target category selected for the branch.
            /// </summary>
            public IpcEditTargetKind Kind { get; }

            /// <summary>
            /// Gets the cloned primitive reference payload used by lowered operations.
            /// </summary>
            public JsonElement Reference { get; }
        }

        private readonly struct SetAssignment
        {
            public SetAssignment (
                string path,
                JsonElement value)
            {
                Path = path;
                Value = value;
            }

            public string Path { get; }

            public JsonElement Value { get; }
        }

        private static string ToActionLiteral (IpcEditStepContract.ActionKind actionKind)
        {
            return actionKind switch
            {
                IpcEditStepContract.ActionKind.Set => "set",
                IpcEditStepContract.ActionKind.EnsureComponent => "ensureComponent",
                IpcEditStepContract.ActionKind.CreateObject => "createObject",
                IpcEditStepContract.ActionKind.CreateAsset => "createAsset",
                IpcEditStepContract.ActionKind.CreatePrefab => "createPrefab",
                IpcEditStepContract.ActionKind.Delete => "delete",
                IpcEditStepContract.ActionKind.Reparent => "reparent",
                _ => actionKind.ToString(),
            };
        }
    }
}
