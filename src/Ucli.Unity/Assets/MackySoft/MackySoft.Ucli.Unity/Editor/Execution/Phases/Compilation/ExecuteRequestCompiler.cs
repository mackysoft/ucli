using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.SceneInspection;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary>
    /// <para> Compiles validated public request steps into primitive-only execute-request models. </para>
    /// <para> <c>kind:"edit"</c> steps are expanded into concrete primitive chains, while <c>kind:"op"</c> steps are forwarded unchanged. </para>
    /// </summary>
    internal sealed class ExecuteRequestCompiler
    {
        private const string EditOperationName = "edit";

        private readonly IPhaseOperationRegistry operationRegistry;

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestCompiler" /> class. </summary>
        /// <param name="operationRegistry"> The operation registry used to validate Play Mode raw operation support. </param>
        public ExecuteRequestCompiler (IPhaseOperationRegistry operationRegistry)
        {
            this.operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
        }

        /// <summary>
        /// Compiles one validated source step against the current request-local execution state.
        /// </summary>
        /// <param name="step"> The validated public source step. </param>
        /// <param name="executionContext"> The current request execution context used for dynamic selection resolution. </param>
        /// <param name="compiledStep"> The compiled public step metadata when compilation succeeds. </param>
        /// <param name="operations"> The compiled primitive operations in execution order when compilation succeeds. </param>
        /// <param name="diagnostics"> Non-fatal diagnostics emitted before compilation succeeds or fails. </param>
        /// <param name="error"> The structured normalization error when compilation fails. </param>
        /// <returns> <see langword="true" /> when the source step can be compiled for the current execution state; otherwise <see langword="false" />. </returns>
        public bool TryCompileExecutionStep (
            IpcExecuteStepContract step,
            OperationExecutionContext executionContext,
            bool allowPlayMode,
            out NormalizedRequestStep compiledStep,
            out IReadOnlyList<NormalizedOperation> operations,
            out IReadOnlyList<OperationDiagnostic> diagnostics,
            out ExecuteRequestNormalizationError error)
        {
            compiledStep = default!;
            operations = Array.Empty<NormalizedOperation>();
            diagnostics = Array.Empty<OperationDiagnostic>();
            var instancePath = GetStepInstancePath(step.Id);

            if (step.Kind == IpcExecuteStepKind.Op)
            {
                if (!RawOperationPlayModeSupportValidator.TryValidate(
                        operationRegistry,
                        step,
                        instancePath,
                        allowPlayMode,
                        out error))
                {
                    return false;
                }

                return TryCompileOpStep(step, out compiledStep, out operations, out error);
            }

            if (step.Kind == IpcExecuteStepKind.Edit)
            {
                return TryCompileEditStep(step, executionContext, allowPlayMode, out compiledStep, out operations, out diagnostics, out error);
            }

            error = ExecuteRequestNormalizationError.InvalidArgument(
                message: $"Request step at '{instancePath}' has unsupported kind.",
                instancePath);
            return false;
        }

        private bool TryCompileOpStep (
            IpcExecuteStepContract step,
            out NormalizedRequestStep compiledStep,
            out IReadOnlyList<NormalizedOperation> operations,
            out ExecuteRequestNormalizationError error)
        {
            compiledStep = default!;
            operations = Array.Empty<NormalizedOperation>();
            var operationName = step.OperationName
                ?? throw new InvalidOperationException("A normalized operation step has no operation name.");
            if (step.OperationArgs.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("A normalized operation step has no argument object.");
            }

            operationRegistry.TryResolve(operationName, out var registeredOperation);
            operations = new[]
            {
                new NormalizedOperation(
                    ExecutionKey: OperationExecutionKey.ForRawStep(step.Id),
                    Op: operationName,
                    Args: step.OperationArgs.Clone(),
                    As: null,
                    Expect: null,
                    AliasReferences: OperationAliasReferenceMap.Empty,
                    PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                    AllowExplicitPrefabAssetMutation: false),
            };
            compiledStep = new NormalizedRequestStep(
                Id: step.Id,
                Kind: IpcExecuteStepKind.Op,
                OperationName: operationName,
                PrimitiveCount: operations.Count,
                OperationDescriptorDigest: registeredOperation?.Metadata.DescriptorDigest)
            {
                PostReadSourceStep = CreateOperationPostReadSourceStep(operationName),
            };
            error = default!;
            return true;
        }

        private bool TryCompileEditStep (
            IpcExecuteStepContract step,
            OperationExecutionContext executionContext,
            bool allowPlayMode,
            out NormalizedRequestStep compiledStep,
            out IReadOnlyList<NormalizedOperation> operations,
            out IReadOnlyList<OperationDiagnostic> diagnostics,
            out ExecuteRequestNormalizationError error)
        {
            compiledStep = default!;
            operations = Array.Empty<NormalizedOperation>();
            diagnostics = Array.Empty<OperationDiagnostic>();
            var editStep = step.EditContract
                ?? throw new InvalidOperationException("A normalized edit step has no execution model.");

            var stepOperations = new List<NormalizedOperation>(editStep.Actions.Count + 4);
            var shouldReleaseImplicitExecutionContextOnNoTargets =
                ShouldReleaseImplicitExecutionContextOnNoTargets(editStep, executionContext);
            if (!TryEnsureImplicitExecutionContext(editStep, executionContext, allowPlayMode, out error))
            {
                return false;
            }

            if (!TryResolveSelection(editStep, executionContext, out var selectedTargets, out diagnostics, out error))
            {
                return false;
            }

            if (selectedTargets.Count > 0)
            {
                if (!TryValidateLiveEditContextAvailability(editStep, executionContext, allowPlayMode, out error))
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
                            allowPlayMode,
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

            if (!TryValidateCommitContextAvailability(editStep, executionContext, allowPlayMode, out error))
            {
                return false;
            }

            if (!TryAddCommitOperation(stepOperations, editStep, allowPlayMode, out error))
            {
                return false;
            }

            compiledStep = new NormalizedRequestStep(
                Id: editStep.Id,
                Kind: IpcExecuteStepKind.Edit,
                OperationName: EditOperationName,
                PrimitiveCount: stepOperations.Count,
                OperationDescriptorDigest: null)
            {
                Diagnostics = diagnostics,
                PostReadSourceStep = CreateEditPostReadSourceStep(editStep, allowPlayMode),
            };
            operations = stepOperations;
            error = default!;
            return true;
        }

        /// <summary> Creates metadata for a source step that will not execute after request-level fail-fast. </summary>
        /// <param name="sourceStep"> The normalized source step. </param>
        /// <returns> Step metadata with no compiled primitives and any fixed direct-operation descriptor digest. </returns>
        public NormalizedRequestStep CreateSkippedStep (IpcExecuteStepContract sourceStep)
        {
            if (sourceStep == null)
            {
                throw new ArgumentNullException(nameof(sourceStep));
            }

            var isDirectOperation = sourceStep.Kind == IpcExecuteStepKind.Op;
            var operationName = isDirectOperation
                ? sourceStep.OperationName
                    ?? throw new InvalidOperationException("A normalized operation step has no operation name.")
                : EditOperationName;
            operationRegistry.TryResolve(operationName, out var registeredOperation);
            return new NormalizedRequestStep(
                Id: sourceStep.Id,
                Kind: sourceStep.Kind,
                OperationName: operationName,
                PrimitiveCount: 0,
                OperationDescriptorDigest: isDirectOperation
                    ? registeredOperation?.Metadata.DescriptorDigest
                    : null);
        }

        private static IpcExecutePostReadSourceStep CreateOperationPostReadSourceStep (
            string operationName)
        {
            return new IpcExecutePostReadSourceStep(
                SourceKind: IpcExecutePostReadSourceKind.Operation,
                PlayModeMutation: false,
                Commit: null,
                PersistenceExpected: false,
                ExpectedPostState: IpcExecuteExpectedPostState.Unavailable);
        }

        private static IpcExecutePostReadSourceStep CreateEditPostReadSourceStep (
            IpcEditStepContract editStep,
            bool allowPlayMode)
        {
            var isPlayModeSceneMutation = allowPlayMode
                && editStep.Context.Kind == IpcEditStepContract.ContextKind.Scene;
            var persistenceExpected = IsPersistenceExpected(editStep);
            return new IpcExecutePostReadSourceStep(
                SourceKind: IpcExecutePostReadSourceKind.Edit,
                PlayModeMutation: isPlayModeSceneMutation,
                Commit: MapPostReadCommit(editStep.Commit),
                PersistenceExpected: persistenceExpected,
                ExpectedPostState: isPlayModeSceneMutation
                    ? IpcExecuteExpectedPostState.Unavailable
                    : IpcExecuteExpectedPostState.Deterministic);
        }

        private static bool IsPersistenceExpected (IpcEditStepContract editStep)
        {
            if (editStep.Commit != IpcEditStepContract.CommitKind.None)
            {
                return true;
            }

            for (var actionIndex = 0; actionIndex < editStep.Actions.Count; actionIndex++)
            {
                var actionKind = editStep.Actions[actionIndex].Kind;
                if (actionKind == IpcEditStepContract.ActionKind.CreateAsset
                    || actionKind == IpcEditStepContract.ActionKind.CreatePrefab
                    || actionKind == IpcEditStepContract.ActionKind.ApplyPrefabOverrides)
                {
                    return true;
                }
            }

            return false;
        }

        private static IpcExecutePostReadCommit MapPostReadCommit (IpcEditStepContract.CommitKind commit)
        {
            switch (commit)
            {
                case IpcEditStepContract.CommitKind.None:
                    return IpcExecutePostReadCommit.None;

                case IpcEditStepContract.CommitKind.Context:
                    return IpcExecutePostReadCommit.Context;

                case IpcEditStepContract.CommitKind.Project:
                    return IpcExecutePostReadCommit.Project;

                default:
                    throw new ArgumentOutOfRangeException(nameof(commit), commit, "Unsupported edit commit kind.");
            }
        }

        private static bool TryValidateLiveEditContextAvailability (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            bool allowPlayMode,
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
                    if (SceneAssetSourceUtilities.TryGetLoadedScene(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLiveSceneOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step mutates scene context '{step.Context.Path}', but the scene is not loaded. Add 'ucli.scene.open' before this step.",
                        GetStepInstancePath(step.Id));
                    return false;

                case IpcEditStepContract.ContextKind.Prefab:
                    if (allowPlayMode)
                    {
                        return true;
                    }

                    if (PrefabOperationUtilities.TryGetOpenedPrefabStage(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLivePrefabOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step mutates prefab context '{step.Context.Path}', but the prefab is not opened. Add 'ucli.prefab.open' before this step.",
                        GetStepInstancePath(step.Id));
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
            bool allowPlayMode,
            out ExecuteRequestNormalizationError error)
        {
            error = default!;
            var operationName = IpcEditStepLoweringRules.GetCommitOperationName(step.Context.Kind, step.Commit, allowPlayMode);
            if (operationName == null)
            {
                return true;
            }

            switch (operationName)
            {
                case UcliPrimitiveOperationNames.SceneSave:
                    if (SceneAssetSourceUtilities.TryGetLoadedScene(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLiveSceneOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step saves scene context '{step.Context.Path}', but the scene is not loaded. Add 'ucli.scene.open' before this step.",
                        GetStepInstancePath(step.Id));
                    return false;

                case UcliPrimitiveOperationNames.PrefabSave:
                    if (allowPlayMode)
                    {
                        return true;
                    }

                    if (PrefabOperationUtilities.TryGetOpenedPrefabStage(step.Context.Path!, out _, out _)
                        || executionContext.HasPlannedLivePrefabOpen(step.Context.Path!))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Edit step saves prefab context '{step.Context.Path}', but the prefab is not opened. Add 'ucli.prefab.open' before this step.",
                        GetStepInstancePath(step.Id));
                    return false;

                default:
                    return true;
            }
        }

        private static bool TryEnsureImplicitExecutionContext (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            bool allowPlayMode,
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
                    if (allowPlayMode)
                    {
                        return true;
                    }

                    if (executionContext.TryEnsureSceneExecutionSession(step.Context.Path!, out var sceneErrorMessage))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        sceneErrorMessage,
                        GetStepInstancePath(step.Id));
                    return false;

                case IpcEditStepContract.ContextKind.Prefab:
                    if (executionContext.TryEnsurePrefabExecutionSession(step.Context.Path!, out var prefabErrorMessage))
                    {
                        return true;
                    }

                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        prefabErrorMessage,
                        GetStepInstancePath(step.Id));
                    return false;

                default:
                    return true;
            }
        }

        private bool TryResolveSelection (
            IpcEditStepContract step,
            OperationExecutionContext executionContext,
            out List<SelectionTarget> selectedTargets,
            out IReadOnlyList<OperationDiagnostic> diagnostics,
            out ExecuteRequestNormalizationError error)
        {
            selectedTargets = new List<SelectionTarget>();
            diagnostics = Array.Empty<OperationDiagnostic>();
            if (step.Selection.Kind == IpcEditStepContract.SelectionKind.Direct)
            {
                if (!TryResolveDirectSelectionTargets(step, executionContext, selectedTargets, out error))
                {
                    return false;
                }
            }
            else
            {
                if (!TryCreateSceneQueryArguments(step, out var queryArguments, out var errorMessage))
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        errorMessage,
                        GetStepInstancePath(step.Id));
                    return false;
                }

                if (!SceneQuerySelectionEngine.TryQueryRuntime(
                        step.Context.Path!,
                        queryArguments,
                        executionContext,
                        allowTemporaryState: true,
                        out var matches,
                        out diagnostics,
                        out errorMessage))
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        errorMessage,
                        GetStepInstancePath(step.Id));
                    return false;
                }

                selectedTargets = new List<SelectionTarget>(matches.Count);
                for (var i = 0; i < matches.Count; i++)
                {
                    selectedTargets.Add(CreateSceneSelectionTarget(step.Context.Path!, matches[i]));
                }
            }

            if (!TryApplyCardinality(
                    GetStepInstancePath(step.Id),
                    step.Selection.Cardinality,
                    selectedTargets,
                    out error))
            {
                return false;
            }

            if (!TryValidateSelectedTargetActions(step, selectedTargets.Count, out error))
            {
                return false;
            }

            return true;
        }

        private static bool TryCreateSceneQueryArguments (
            IpcEditStepContract step,
            out SceneQuerySelectionEngine.QueryArguments queryArguments,
            out string errorMessage)
        {
            queryArguments = default;
            if (step.Context.Kind != IpcEditStepContract.ContextKind.Scene)
            {
                errorMessage = "Edit step query selection is supported only for scene context.";
                return false;
            }

            UnityComponentTypeId? componentTypeId = null;
            Type? componentRuntimeType = null;
            if (step.Selection.SourceComponentType != null)
            {
                componentTypeId = new UnityComponentTypeId(step.Selection.SourceComponentType);
                if (!ComponentTypeResolver.TryResolveComponentType(componentTypeId.Value, out componentRuntimeType, out errorMessage))
                {
                    return false;
                }
            }

            queryArguments = new SceneQuerySelectionEngine.QueryArguments(
                step.Selection.SourcePathPrefix,
                componentTypeId,
                componentRuntimeType);
            errorMessage = string.Empty;
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
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    GetStepInstancePath(step.Id));
                return false;
            }

            if (!TryResolveDirectSelectionTargetPresence(target, executionContext, out var hasMatch, out errorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    errorMessage,
                    GetStepInstancePath(step.Id));
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
            if (!UnityObjectReferenceCodec.TryParse(
                    target.Reference,
                    "select",
                    OperationAliasReferenceMap.Empty,
                    out var reference,
                    out errorMessage))
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
            string instancePath,
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
                            "Edit step cardinality 'one' requires exactly one target.",
                            instancePath);
                        return false;
                    }

                    break;

                case IpcEditStepContract.CardinalityKind.First:
                    if (selectedTargets.Count == 0)
                    {
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            "Edit step cardinality 'first' requires at least one target.",
                            instancePath);
                        return false;
                    }

                    selectedTargets.RemoveRange(1, selectedTargets.Count - 1);
                    break;

                case IpcEditStepContract.CardinalityKind.AtMostOne:
                    if (selectedTargets.Count > 1)
                    {
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            "Edit step cardinality 'atMostOne' requires zero or one target.",
                            instancePath);
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
                    $"Edit action '{Vocabulary.GetText(actionKind)}' requires the selection to resolve to at most one target.",
                    GetStepInstancePath(step.Id));
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
            bool allowPlayMode,
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
                    allowPlayMode,
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
            bool allowPlayMode,
            out ExecuteRequestNormalizationError error)
        {
            switch (action.Kind)
            {
                case IpcEditStepContract.ActionKind.Set:
                    return TryCompileSetAction(step, branchTarget, aliases, action, operations, allowPlayMode, out error);

                case IpcEditStepContract.ActionKind.EnsureComponent:
                    return TryCompileEnsureComponentAction(step, branchIndex, branchTarget, aliases, action, operations, allowPlayMode, out error);

                case IpcEditStepContract.ActionKind.CreateObject:
                    return TryCompileCreateObjectAction(step, branchIndex, branchTarget, aliases, action, operations, allowPlayMode, out error);

                case IpcEditStepContract.ActionKind.CreateAsset:
                    return TryCompileCreateAssetAction(step, action, operations, out error);

                case IpcEditStepContract.ActionKind.CreatePrefab:
                    return TryCompileCreatePrefabAction(step, branchTarget, aliases, action, operations, allowPlayMode, out error);

                case IpcEditStepContract.ActionKind.ApplyPrefabOverrides:
                    return TryCompilePrefabOverrideAction(step, branchTarget, aliases, action, operations, allowPlayMode, isRevert: false, out error);

                case IpcEditStepContract.ActionKind.RevertPrefabOverrides:
                    return TryCompilePrefabOverrideAction(step, branchTarget, aliases, action, operations, allowPlayMode, isRevert: true, out error);

                case IpcEditStepContract.ActionKind.Delete:
                    return TryCompileDeleteAction(step, branchTarget, aliases, action, operations, allowPlayMode, out error);

                case IpcEditStepContract.ActionKind.Reparent:
                    return TryCompileReparentAction(step, branchTarget, aliases, action, operations, allowPlayMode, out error);

                default:
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        $"Unsupported edit action kind '{action.Kind}'.",
                        GetStepInstancePath(step.Id));
                    return false;
            }
        }

        private static bool TryCompileSetAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            bool allowPlayMode,
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreateSetArgs(target.Reference, action.Values),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(target.AliasIdentity),
                PersistenceReportingPolicy: ShouldSuppressPlayModeLivePersistence(step, allowPlayMode)
                    ? OperationPersistenceReportingPolicy.SuppressAll
                    : OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: ShouldAllowExplicitPrefabAssetMutation(step, allowPlayMode)));
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
            bool allowPlayMode,
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            var internalAlias = CreateInternalAlias(step.Id, branchIndex, action.Alias);
            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreateEnsureComponentArgs(target.Reference, action.Type!),
                As: internalAlias,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(target.AliasIdentity),
                PersistenceReportingPolicy: ShouldSuppressPlayModeLivePersistence(step, allowPlayMode)
                    ? OperationPersistenceReportingPolicy.SuppressAll
                    : OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));

            if (action.Alias != null)
            {
                aliases[action.Alias] = new SelectionTarget(
                    IpcEditTargetKind.Component,
                    CreateAliasReference(internalAlias!.Alias),
                    internalAlias);
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
            bool allowPlayMode,
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            var internalAlias = CreateInternalAlias(step.Id, branchIndex, action.Alias);
            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreateGoCreateArgs(action.Name!, branchTarget.Reference),
                As: internalAlias,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(branchTarget.AliasIdentity),
                PersistenceReportingPolicy: ShouldSuppressPlayModeLivePersistence(step, allowPlayMode)
                    ? OperationPersistenceReportingPolicy.SuppressAll
                    : OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));

            if (action.Alias != null)
            {
                aliases[action.Alias] = new SelectionTarget(
                    IpcEditTargetKind.GameObject,
                    CreateAliasReference(internalAlias!.Alias),
                    internalAlias);
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreateAssetCreateArgs(action.Type!, action.Path!),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));
            error = default!;
            return true;
        }

        private static bool TryCompileCreatePrefabAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            bool allowPlayMode,
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreatePrefabCreateArgs(target.Reference, action.Path!),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(target.AliasIdentity),
                PersistenceReportingPolicy: ShouldSuppressPlayModeLivePersistence(step, allowPlayMode)
                    ? OperationPersistenceReportingPolicy.SuppressScene
                    : OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));
            error = default!;
            return true;
        }

        private static bool TryCompilePrefabOverrideAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            bool allowPlayMode,
            bool isRevert,
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreatePrefabOverrideArgs(target.Reference, action.TargetAssetPath!, action.PropertyPaths),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(target.AliasIdentity),
                PersistenceReportingPolicy: isRevert && allowPlayMode
                    ? OperationPersistenceReportingPolicy.SuppressAll
                    : OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));
            error = default!;
            return true;
        }

        private static bool TryCompileDeleteAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            bool allowPlayMode,
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreateDeleteArgs(target.Reference),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(target.AliasIdentity),
                PersistenceReportingPolicy: ShouldSuppressPlayModeLivePersistence(step, allowPlayMode)
                    ? OperationPersistenceReportingPolicy.SuppressAll
                    : OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));
            error = default!;
            return true;
        }

        private static bool TryCompileReparentAction (
            IpcEditStepContract step,
            SelectionTarget branchTarget,
            IDictionary<string, SelectionTarget> aliases,
            IpcEditStepContract.EditAction action,
            ICollection<NormalizedOperation> operations,
            bool allowPlayMode,
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
                    GetStepInstancePath(step.Id));
                return false;
            }

            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: CreateReparentArgs(target.Reference, parent.Reference),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Create(target.AliasIdentity, parent.AliasIdentity),
                PersistenceReportingPolicy: ShouldSuppressPlayModeLivePersistence(step, allowPlayMode)
                    ? OperationPersistenceReportingPolicy.SuppressAll
                    : OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));
            error = default!;
            return true;
        }

        private static bool TryAddCommitOperation (
            ICollection<NormalizedOperation> operations,
            IpcEditStepContract step,
            bool allowPlayMode,
            out ExecuteRequestNormalizationError error)
        {
            var operationName = IpcEditStepLoweringRules.GetCommitOperationName(step.Context.Kind, step.Commit, allowPlayMode);
            if (operationName == null)
            {
                error = default!;
                return true;
            }

            JsonElement args;
            if (allowPlayMode
                && operationName == UcliPrimitiveOperationNames.AssetSave
                && TryCreatePlayModeTargetSaveOperation(step, out var playModeSaveArgs))
            {
                args = playModeSaveArgs;
            }
            else
            {
                args = operationName == UcliPrimitiveOperationNames.ProjectSave
                    ? CreateEmptyArgs()
                    : CreatePathArgs(step.Context.Path!);
            }

            operations.Add(new NormalizedOperation(
                ExecutionKey: CreateEditPrimitiveExecutionKey(step.Id, operations.Count),
                Op: operationName,
                Args: args,
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false));
            error = default!;
            return true;
        }

        private static bool TryCreatePlayModeTargetSaveOperation (
            IpcEditStepContract step,
            out JsonElement args)
        {
            switch (step.Context.Kind)
            {
                case IpcEditStepContract.ContextKind.Asset:
                    args = CreateAssetSaveArgs(CreateAssetPathSelector(step.Context.Path!));
                    return true;

                case IpcEditStepContract.ContextKind.Project:
                    args = CreateAssetSaveArgs(CreateProjectAssetPathSelector(step.Selection.ProjectAssetPath!));
                    return true;

                default:
                    args = default;
                    return false;
            }
        }

        private static bool ShouldSuppressPlayModeLivePersistence (
            IpcEditStepContract step,
            bool allowPlayMode)
        {
            return allowPlayMode && step.Context.Kind == IpcEditStepContract.ContextKind.Scene;
        }

        private static bool ShouldAllowExplicitPrefabAssetMutation (
            IpcEditStepContract step,
            bool allowPlayMode)
        {
            return allowPlayMode && step.Context.Kind == IpcEditStepContract.ContextKind.Scene;
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
                        CreateSceneSelector(step.Context.Path!, step.Selection.GameObjectPath!, step.Selection.ComponentType),
                        aliasIdentity: null);
                    errorMessage = string.Empty;
                    return true;
                case IpcEditStepContract.ContextKind.Prefab:
                    target = new SelectionTarget(
                        IpcEditStepLoweringRules.DetermineDirectSelectionTargetKind(step.Context.Kind, step.Selection.ComponentType),
                        CreatePrefabSelector(step.Context.Path!, step.Selection.GameObjectPath!, step.Selection.ComponentType),
                        aliasIdentity: null);
                    errorMessage = string.Empty;
                    return true;
                case IpcEditStepContract.ContextKind.Asset:
                    target = new SelectionTarget(
                        IpcEditTargetKind.Asset,
                        CreateAssetPathSelector(step.Context.Path!),
                        aliasIdentity: null);
                    errorMessage = string.Empty;
                    return true;
                case IpcEditStepContract.ContextKind.Project:
                    target = new SelectionTarget(
                        IpcEditTargetKind.Asset,
                        CreateProjectAssetPathSelector(step.Selection.ProjectAssetPath!),
                        aliasIdentity: null);
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
            var targetKind = match.TargetKind == SceneQuerySelectionEngine.QueryTargetKind.Component
                ? IpcEditTargetKind.Component
                : IpcEditTargetKind.GameObject;

            return new SelectionTarget(
                targetKind,
                CreateSceneSelector(
                    scenePath,
                    match.HierarchyPath,
                    match.ComponentType?.Value),
                aliasIdentity: null);
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
                    GetStepInstancePath(step.Id));
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
                        GetStepInstancePath(step.Id));
                    return false;
                }

                error = default!;
                return true;
            }

            if (!IpcEditStepLoweringRules.SupportsDirectHierarchyParentLiteral(step.Context.Kind))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    "Edit action 'reparent' can use direct parent paths only in scene or prefab context.",
                    GetStepInstancePath(step.Id));
                target = default;
                return false;
            }

            if (step.Context.Kind == IpcEditStepContract.ContextKind.Scene)
            {
                target = new SelectionTarget(
                    IpcEditTargetKind.GameObject,
                    CreateSceneSelector(step.Context.Path!, parentLiteral, null),
                    aliasIdentity: null);
                error = default!;
                return true;
            }

            target = new SelectionTarget(
                IpcEditTargetKind.GameObject,
                CreatePrefabSelector(step.Context.Path!, parentLiteral, null),
                aliasIdentity: null);
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

        private static RequestLocalAliasIdentity.EditActionAliasIdentity? CreateInternalAlias (
            IpcExecuteStepId stepId,
            int branchIndex,
            string? alias)
        {
            if (alias == null)
            {
                return null;
            }

            return RequestLocalAliasIdentity.ForEditAction(
                stepId,
                branchIndex,
                new UcliPlanAlias(alias));
        }

        private static OperationExecutionKey CreateEditPrimitiveExecutionKey (
            IpcExecuteStepId stepId,
            int primitiveIndex)
        {
            return OperationExecutionKey.ForEditPrimitive(stepId, primitiveIndex);
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

        private static JsonElement CreateAliasReference (UcliPlanAlias alias)
        {
            return IpcPayloadCodec.SerializeToElement<UnityObjectReferenceArgs>(
                new UcliAliasReferenceArgs(alias));
        }

        private static JsonElement CreateAssetPathSelector (string assetPath)
        {
            return IpcPayloadCodec.SerializeToElement<UnityObjectReferenceArgs>(
                new AssetPathReferenceArgs(new UnityAssetPath(assetPath)));
        }

        private static JsonElement CreateAssetSaveArgs (JsonElement targetReference)
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

        private static JsonElement CreateProjectAssetPathSelector (string assetPath)
        {
            return IpcPayloadCodec.SerializeToElement<UnityObjectReferenceArgs>(
                new ProjectAssetPathReferenceArgs(new ProjectSettingsAssetPath(assetPath)));
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
            var scene = new SceneAssetPath(scenePath);
            var hierarchy = new UnityHierarchyPath(hierarchyPath);
            UnityObjectReferenceArgs reference = string.IsNullOrWhiteSpace(componentType)
                ? new SceneHierarchyReferenceArgs(scene, hierarchy)
                : new SceneComponentReferenceArgs(scene, hierarchy, new UnityComponentTypeId(componentType));
            return IpcPayloadCodec.SerializeToElement(reference);
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
            var prefab = new PrefabAssetPath(prefabPath);
            var hierarchy = new UnityHierarchyPath(hierarchyPath);
            UnityObjectReferenceArgs reference = string.IsNullOrWhiteSpace(componentType)
                ? new PrefabHierarchyReferenceArgs(prefab, hierarchy)
                : new PrefabComponentReferenceArgs(prefab, hierarchy, new UnityComponentTypeId(componentType));
            return IpcPayloadCodec.SerializeToElement(reference);
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

        private static JsonElement CreatePrefabOverrideArgs (
            JsonElement targetReference,
            string targetAssetPath,
            IReadOnlyList<string>? propertyPaths)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("target");
                targetReference.WriteTo(writer);
                writer.WriteString("targetAssetPath", targetAssetPath);
                if (propertyPaths != null)
                {
                    writer.WritePropertyName("propertyPaths");
                    writer.WriteStartArray();
                    for (var i = 0; i < propertyPaths.Count; i++)
                    {
                        writer.WriteStringValue(propertyPaths[i]);
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return ParseElement(stream);
        }

        private static JsonElement CreateGoCreateArgs (
            string name,
            JsonElement parentReference)
        {
            if (!IpcPayloadCodec.TryDeserializeStrict(
                    parentReference,
                    out GameObjectReferenceArgs parent,
                    out var readError))
            {
                throw new InvalidOperationException(
                    $"Compiled parent reference does not match the GameObject reference contract. {readError.Message}");
            }

            return IpcPayloadCodec.SerializeToElement<GoCreateArgs>(
                new GoCreateUnderParentArgs(name, parent));
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

        private static string GetStepInstancePath (IpcExecuteStepId stepId)
        {
            return $"/steps/{stepId.Value}";
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
                JsonElement reference,
                RequestLocalAliasIdentity.EditActionAliasIdentity? aliasIdentity)
            {
                Kind = kind;
                Reference = reference;
                AliasIdentity = aliasIdentity;
            }

            /// <summary>
            /// Gets the primitive target category selected for the branch.
            /// </summary>
            public IpcEditTargetKind Kind { get; }

            /// <summary>
            /// Gets the cloned primitive reference payload used by lowered operations.
            /// </summary>
            public JsonElement Reference { get; }

            /// <summary> Gets the typed internal alias identity when the reference uses an edit-action alias. </summary>
            public RequestLocalAliasIdentity.EditActionAliasIdentity? AliasIdentity { get; }
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

    }
}
