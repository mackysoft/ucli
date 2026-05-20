using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Ipc.EditSteps;

/// <summary> Defines shared structural lowering rules for public <c>kind:"edit"</c> steps. </summary>
internal static class IpcEditStepLoweringRules
{
    /// <summary>
    /// Determines whether one edit step needs a live editable scene or prefab context for its actions.
    /// </summary>
    /// <param name="stepContract"> The validated edit-step contract. </param>
    /// <returns> <see langword="true" /> when at least one action requires a live editable context; otherwise <see langword="false" />. </returns>
    public static bool RequiresLiveEditableContext (IpcEditStepContract stepContract)
    {
        if (stepContract == null)
        {
            throw new ArgumentNullException(nameof(stepContract));
        }

        for (var actionIndex = 0; actionIndex < stepContract.Actions.Count; actionIndex++)
        {
            if (stepContract.Actions[actionIndex].Kind != IpcEditStepContract.ActionKind.CreateAsset)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the selection-source operation referenced by one <c>select.from</c> declaration.
    /// </summary>
    /// <param name="selection"> The validated edit selection contract. </param>
    /// <returns> The referenced selection-source operation name, or <see langword="null" /> for direct selection. </returns>
    public static string? GetSelectionSourceOperationName (IpcEditStepContract.EditSelection selection)
    {
        return selection.Kind == IpcEditStepContract.SelectionKind.From
            ? selection.SourceOperation
            : null;
    }

    /// <summary>
    /// Determines the structural target category implied by one validated edit selection.
    /// </summary>
    /// <param name="stepContract"> The validated edit-step contract. </param>
    /// <returns> The target category that action validation should assume before runtime selection resolution. </returns>
    public static IpcEditTargetKind DetermineStructuralSelectionTargetKind (IpcEditStepContract stepContract)
    {
        if (stepContract.Context.Kind == IpcEditStepContract.ContextKind.Asset
            || stepContract.Context.Kind == IpcEditStepContract.ContextKind.Project)
        {
            return IpcEditTargetKind.Asset;
        }

        if (stepContract.Selection.Kind == IpcEditStepContract.SelectionKind.Direct)
        {
            return stepContract.Selection.ComponentType is not null
                ? IpcEditTargetKind.Component
                : IpcEditTargetKind.GameObject;
        }

        return stepContract.Selection.SourceArgs.ValueKind == JsonValueKind.Object
               && stepContract.Selection.SourceArgs.TryGetProperty("componentType", out _)
            ? IpcEditTargetKind.Component
            : IpcEditTargetKind.GameObject;
    }

    /// <summary>
    /// Determines the target category implied by one direct edit selection.
    /// </summary>
    /// <param name="contextKind"> The edit context kind. </param>
    /// <param name="componentType"> The optional direct component type selector. </param>
    /// <returns> The target category selected by the direct selector. </returns>
    public static IpcEditTargetKind DetermineDirectSelectionTargetKind (
        IpcEditStepContract.ContextKind contextKind,
        string? componentType)
    {
        switch (contextKind)
        {
            case IpcEditStepContract.ContextKind.Asset:
            case IpcEditStepContract.ContextKind.Project:
                return IpcEditTargetKind.Asset;

            default:
                return componentType is not null
                    ? IpcEditTargetKind.Component
                    : IpcEditTargetKind.GameObject;
        }
    }

    /// <summary>
    /// Gets the implicit save primitive operation name required for the specified commit mode.
    /// </summary>
    /// <param name="contextKind"> The edit context kind. </param>
    /// <param name="commitKind"> The requested commit mode. </param>
    /// <returns> The implicit save primitive operation name, or <see langword="null" /> when no save operation is required. </returns>
    public static string? GetCommitOperationName (
        IpcEditStepContract.ContextKind contextKind,
        IpcEditStepContract.CommitKind commitKind)
    {
        switch (commitKind)
        {
            case IpcEditStepContract.CommitKind.None:
                return null;

            case IpcEditStepContract.CommitKind.Context:
                switch (contextKind)
                {
                    case IpcEditStepContract.ContextKind.Scene:
                        return UcliPrimitiveOperationNames.SceneSave;

                    case IpcEditStepContract.ContextKind.Prefab:
                        return UcliPrimitiveOperationNames.PrefabSave;

                    default:
                        return UcliPrimitiveOperationNames.ProjectSave;
                }

            case IpcEditStepContract.CommitKind.Project:
                return UcliPrimitiveOperationNames.ProjectSave;

            default:
                return null;
        }
    }

    /// <summary>
    /// Determines whether the specified edit context accepts direct hierarchy-path parent literals.
    /// </summary>
    /// <param name="contextKind"> The edit context kind. </param>
    /// <returns> <see langword="true" /> when direct hierarchy-path parent literals are valid in the context; otherwise <see langword="false" />. </returns>
    public static bool SupportsDirectHierarchyParentLiteral (IpcEditStepContract.ContextKind contextKind)
    {
        return contextKind == IpcEditStepContract.ContextKind.Scene
               || contextKind == IpcEditStepContract.ContextKind.Prefab;
    }

    /// <summary>
    /// Resolves the primitive operation name required for one validated edit action and target combination.
    /// </summary>
    /// <param name="actionKind"> The edit action kind. </param>
    /// <param name="targetKind"> The resolved target category for the action. </param>
    /// <param name="parentTargetKind"> The resolved parent target category for <c>reparent</c>. <see langword="null" /> for actions that do not use a parent target. </param>
    /// <param name="operationName"> The required primitive operation name when validation succeeds. </param>
    /// <param name="errorMessage"> The structural validation error message when validation fails. </param>
    /// <returns> <see langword="true" /> when the action can be lowered for the supplied target categories; otherwise <see langword="false" />. </returns>
    public static bool TryGetActionOperationName (
        IpcEditStepContract.ContextKind contextKind,
        IpcEditStepContract.ActionKind actionKind,
        IpcEditTargetKind targetKind,
        IpcEditTargetKind? parentTargetKind,
        out string operationName,
        out string errorMessage)
    {
        switch (actionKind)
        {
            case IpcEditStepContract.ActionKind.Set:
                if (targetKind == IpcEditTargetKind.Component)
                {
                    operationName = UcliPrimitiveOperationNames.CompSet;
                    errorMessage = string.Empty;
                    return true;
                }

                if (targetKind == IpcEditTargetKind.Asset)
                {
                    operationName = UcliPrimitiveOperationNames.AssetSet;
                    errorMessage = string.Empty;
                    return true;
                }

                operationName = string.Empty;
                errorMessage = "Edit action 'set' requires a component or asset target.";
                return false;

            case IpcEditStepContract.ActionKind.EnsureComponent:
                if (targetKind == IpcEditTargetKind.GameObject)
                {
                    operationName = UcliPrimitiveOperationNames.CompEnsure;
                    errorMessage = string.Empty;
                    return true;
                }

                operationName = string.Empty;
                errorMessage = "Edit action 'ensureComponent' requires a GameObject target.";
                return false;

            case IpcEditStepContract.ActionKind.CreateObject:
                if (targetKind == IpcEditTargetKind.GameObject)
                {
                    operationName = UcliPrimitiveOperationNames.GoCreate;
                    errorMessage = string.Empty;
                    return true;
                }

                operationName = string.Empty;
                errorMessage = "Edit action 'createObject' requires a GameObject selection context.";
                return false;

            case IpcEditStepContract.ActionKind.CreateAsset:
                operationName = UcliPrimitiveOperationNames.AssetCreate;
                errorMessage = string.Empty;
                return true;

            case IpcEditStepContract.ActionKind.CreatePrefab:
                if (targetKind != IpcEditTargetKind.GameObject)
                {
                    operationName = string.Empty;
                    errorMessage = "Edit action 'createPrefab' requires a GameObject target.";
                    return false;
                }

                if (contextKind == IpcEditStepContract.ContextKind.Scene)
                {
                    operationName = UcliPrimitiveOperationNames.PrefabCreate;
                    errorMessage = string.Empty;
                    return true;
                }

                operationName = string.Empty;
                errorMessage = "Edit action 'createPrefab' requires a GameObject target in scene context.";
                return false;

            case IpcEditStepContract.ActionKind.ApplyPrefabOverrides:
                if (contextKind == IpcEditStepContract.ContextKind.Scene
                    && targetKind == IpcEditTargetKind.Component)
                {
                    operationName = UcliPrimitiveOperationNames.PrefabApplyOverrides;
                    errorMessage = string.Empty;
                    return true;
                }

                operationName = string.Empty;
                errorMessage = "Edit action 'applyPrefabOverrides' requires a component target in scene context.";
                return false;

            case IpcEditStepContract.ActionKind.RevertPrefabOverrides:
                if (contextKind == IpcEditStepContract.ContextKind.Scene
                    && targetKind == IpcEditTargetKind.Component)
                {
                    operationName = UcliPrimitiveOperationNames.PrefabRevertOverrides;
                    errorMessage = string.Empty;
                    return true;
                }

                operationName = string.Empty;
                errorMessage = "Edit action 'revertPrefabOverrides' requires a component target in scene context.";
                return false;

            case IpcEditStepContract.ActionKind.Delete:
                if (targetKind == IpcEditTargetKind.GameObject)
                {
                    operationName = UcliPrimitiveOperationNames.GoDelete;
                    errorMessage = string.Empty;
                    return true;
                }

                operationName = string.Empty;
                errorMessage = "Edit action 'delete' requires a GameObject target.";
                return false;

            case IpcEditStepContract.ActionKind.Reparent:
                if (targetKind != IpcEditTargetKind.GameObject)
                {
                    operationName = string.Empty;
                    errorMessage = "Edit action 'reparent' requires a GameObject target.";
                    return false;
                }

                if (parentTargetKind != IpcEditTargetKind.GameObject)
                {
                    operationName = string.Empty;
                    errorMessage = "Edit action 'reparent' requires a GameObject parent target.";
                    return false;
                }

                operationName = UcliPrimitiveOperationNames.GoReparent;
                errorMessage = string.Empty;
                return true;

            default:
                operationName = string.Empty;
                errorMessage = $"Unsupported edit action kind '{actionKind}'.";
                return false;
        }
    }
}
