using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Text;

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
    /// <param name="contextKind"> The edit context kind. </param>
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
        return actionKind switch
        {
            IpcEditStepContract.ActionKind.Set => TryResolveSet(targetKind, out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.EnsureComponent => TryResolveGameObjectTarget(targetKind, UcliPrimitiveOperationNames.CompEnsure, ActionMessage(actionKind, "requires a GameObject target"), out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.CreateObject => TryResolveGameObjectTarget(targetKind, UcliPrimitiveOperationNames.GoCreate, ActionMessage(actionKind, "requires a GameObject selection context"), out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.CreateAsset => Succeed(UcliPrimitiveOperationNames.AssetCreate, out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.CreatePrefab => TryResolveCreatePrefab(contextKind, targetKind, out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.ApplyPrefabOverrides => TryResolveSceneComponent(contextKind, targetKind, UcliPrimitiveOperationNames.PrefabApplyOverrides, ActionMessage(actionKind, "requires a component target in scene context"), out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.RevertPrefabOverrides => TryResolveSceneComponent(contextKind, targetKind, UcliPrimitiveOperationNames.PrefabRevertOverrides, ActionMessage(actionKind, "requires a component target in scene context"), out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.Delete => TryResolveGameObjectTarget(targetKind, UcliPrimitiveOperationNames.GoDelete, ActionMessage(actionKind, "requires a GameObject target"), out operationName, out errorMessage),
            IpcEditStepContract.ActionKind.Reparent => TryResolveReparent(targetKind, parentTargetKind, out operationName, out errorMessage),
            _ => Fail($"Unsupported edit action kind '{actionKind}'.", out operationName, out errorMessage),
        };
    }

    private static bool TryResolveSet (
        IpcEditTargetKind targetKind,
        out string operationName,
        out string errorMessage)
    {
        return targetKind switch
        {
            IpcEditTargetKind.Component => Succeed(UcliPrimitiveOperationNames.CompSet, out operationName, out errorMessage),
            IpcEditTargetKind.Asset => Succeed(UcliPrimitiveOperationNames.AssetSet, out operationName, out errorMessage),
            _ => Fail(ActionMessage(IpcEditStepContract.ActionKind.Set, "requires a component or asset target"), out operationName, out errorMessage),
        };
    }

    private static bool TryResolveCreatePrefab (
        IpcEditStepContract.ContextKind contextKind,
        IpcEditTargetKind targetKind,
        out string operationName,
        out string errorMessage)
    {
        if (targetKind != IpcEditTargetKind.GameObject)
        {
            return Fail(ActionMessage(IpcEditStepContract.ActionKind.CreatePrefab, "requires a GameObject target"), out operationName, out errorMessage);
        }

        return contextKind == IpcEditStepContract.ContextKind.Scene
            ? Succeed(UcliPrimitiveOperationNames.PrefabCreate, out operationName, out errorMessage)
            : Fail(ActionMessage(IpcEditStepContract.ActionKind.CreatePrefab, "requires a GameObject target in scene context"), out operationName, out errorMessage);
    }

    private static bool TryResolveSceneComponent (
        IpcEditStepContract.ContextKind contextKind,
        IpcEditTargetKind targetKind,
        string primitiveOperationName,
        string failureMessage,
        out string operationName,
        out string errorMessage)
    {
        return contextKind == IpcEditStepContract.ContextKind.Scene && targetKind == IpcEditTargetKind.Component
            ? Succeed(primitiveOperationName, out operationName, out errorMessage)
            : Fail(failureMessage, out operationName, out errorMessage);
    }

    private static bool TryResolveGameObjectTarget (
        IpcEditTargetKind targetKind,
        string primitiveOperationName,
        string failureMessage,
        out string operationName,
        out string errorMessage)
    {
        return targetKind == IpcEditTargetKind.GameObject
            ? Succeed(primitiveOperationName, out operationName, out errorMessage)
            : Fail(failureMessage, out operationName, out errorMessage);
    }

    private static bool TryResolveReparent (
        IpcEditTargetKind targetKind,
        IpcEditTargetKind? parentTargetKind,
        out string operationName,
        out string errorMessage)
    {
        if (targetKind != IpcEditTargetKind.GameObject)
        {
            return Fail(ActionMessage(IpcEditStepContract.ActionKind.Reparent, "requires a GameObject target"), out operationName, out errorMessage);
        }

        return parentTargetKind == IpcEditTargetKind.GameObject
            ? Succeed(UcliPrimitiveOperationNames.GoReparent, out operationName, out errorMessage)
            : Fail(ActionMessage(IpcEditStepContract.ActionKind.Reparent, "requires a GameObject parent target"), out operationName, out errorMessage);
    }

    private static string ActionMessage (
        IpcEditStepContract.ActionKind actionKind,
        string message)
    {
        return $"Edit action '{ContractLiteralCodec.ToValue(actionKind)}' {message}.";
    }

    private static bool Succeed (
        string primitiveOperationName,
        out string operationName,
        out string errorMessage)
    {
        operationName = primitiveOperationName;
        errorMessage = string.Empty;
        return true;
    }

    private static bool Fail (
        string message,
        out string operationName,
        out string errorMessage)
    {
        operationName = string.Empty;
        errorMessage = message;
        return false;
    }
}
