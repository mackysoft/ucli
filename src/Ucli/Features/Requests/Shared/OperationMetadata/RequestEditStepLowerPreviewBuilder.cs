using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Builds one structural primitive-operation preview for an <c>edit</c> step. </summary>
internal static class RequestEditStepLowerPreviewBuilder
{
    /// <summary> Builds one referenced-operation preview for one public <c>kind:"edit"</c> step. </summary>
    /// <param name="stepElement"> The raw edit-step JSON element. </param>
    /// <param name="operationNames"> The primitive operation names referenced by the step when structural lowering succeeds. </param>
    /// <param name="errorMessage"> The structural validation error message when the step cannot be lowered. </param>
    /// <returns> <see langword="true" /> when the edit step can be structurally lowered into primitive operation names; otherwise <see langword="false" />. </returns>
    public static bool TryBuild (
        JsonElement stepElement,
        out IReadOnlyList<string> operationNames,
        out string errorMessage)
    {
        operationNames = Array.Empty<string>();
        errorMessage = string.Empty;
        if (!IpcEditStepContractReader.TryRead(stepElement, out var stepContract, out errorMessage))
        {
            return false;
        }

        var operations = new List<string>(stepContract.Actions.Count + 3);
        AddIfPresent(IpcEditStepLoweringRules.GetSelectionSourceOperationName(stepContract.Selection), operations);

        var currentTargetKind = IpcEditStepLoweringRules.DetermineStructuralSelectionTargetKind(stepContract);
        var aliases = new Dictionary<string, IpcEditTargetKind>(StringComparer.Ordinal);
        for (var actionIndex = 0; actionIndex < stepContract.Actions.Count; actionIndex++)
        {
            var action = stepContract.Actions[actionIndex];
            if (!TryAddActionOperationNames(
                stepContract,
                action,
                currentTargetKind,
                aliases,
                operations,
                out errorMessage))
            {
                return false;
            }
        }

        AddIfPresent(IpcEditStepLoweringRules.GetCommitOperationName(stepContract.Context.Kind, stepContract.Commit), operations);
        operationNames = operations;
        return true;
    }

    private static void AddIfPresent (
        string? operationName,
        List<string> operations)
    {
        if (!string.IsNullOrWhiteSpace(operationName))
        {
            operations.Add(operationName);
        }
    }

    private static bool TryAddActionOperationNames (
        IpcEditStepContract stepContract,
        IpcEditStepContract.EditAction action,
        IpcEditTargetKind currentTargetKind,
        Dictionary<string, IpcEditTargetKind> aliases,
        List<string> operations,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        switch (action.Kind)
        {
            case IpcEditStepContract.ActionKind.Set:
                if (!TryResolveTargetKind(action.Target, currentTargetKind, aliases, out var setTargetKind, out errorMessage))
                {
                    return false;
                }

                return TryAddOperationName(stepContract.Context.Kind, action.Kind, setTargetKind, parentTargetKind: null, operations, out errorMessage);

            case IpcEditStepContract.ActionKind.EnsureComponent:
                if (!TryResolveTargetKind(action.Target, currentTargetKind, aliases, out var ensureTargetKind, out errorMessage))
                {
                    return false;
                }

                if (!TryAddOperationName(stepContract.Context.Kind, action.Kind, ensureTargetKind, parentTargetKind: null, operations, out errorMessage))
                {
                    return false;
                }

                RegisterAlias(action.Alias, IpcEditTargetKind.Component, aliases);
                return true;

            case IpcEditStepContract.ActionKind.CreateObject:
                if (!TryAddOperationName(stepContract.Context.Kind, action.Kind, currentTargetKind, parentTargetKind: null, operations, out errorMessage))
                {
                    return false;
                }

                RegisterAlias(action.Alias, IpcEditTargetKind.GameObject, aliases);
                return true;

            case IpcEditStepContract.ActionKind.CreateAsset:
                return TryAddOperationName(stepContract.Context.Kind, action.Kind, currentTargetKind, parentTargetKind: null, operations, out errorMessage);

            case IpcEditStepContract.ActionKind.CreatePrefab:
                if (!TryResolveTargetKind(action.Target, currentTargetKind, aliases, out var prefabTargetKind, out errorMessage))
                {
                    return false;
                }

                return TryAddOperationName(stepContract.Context.Kind, action.Kind, prefabTargetKind, parentTargetKind: null, operations, out errorMessage);

            case IpcEditStepContract.ActionKind.Delete:
                if (!TryResolveTargetKind(action.Target, currentTargetKind, aliases, out var deleteTargetKind, out errorMessage))
                {
                    return false;
                }

                return TryAddOperationName(stepContract.Context.Kind, action.Kind, deleteTargetKind, parentTargetKind: null, operations, out errorMessage);

            case IpcEditStepContract.ActionKind.Reparent:
                if (!TryResolveTargetKind(action.Target, currentTargetKind, aliases, out var reparentTargetKind, out errorMessage))
                {
                    return false;
                }

                IpcEditTargetKind? parentTargetKind = IpcEditTargetKind.GameObject;
                if (IsAliasReference(action.Parent))
                {
                    if (!TryResolveAlias(action.Parent!, aliases, out var resolvedParentTargetKind, out errorMessage))
                    {
                        return false;
                    }

                    parentTargetKind = resolvedParentTargetKind;
                }
                else if (!IpcEditStepLoweringRules.SupportsDirectHierarchyParentLiteral(stepContract.Context.Kind))
                {
                    errorMessage = "Edit action 'reparent' can use direct parent paths only in scene or prefab context.";
                    return false;
                }

                return TryAddOperationName(stepContract.Context.Kind, action.Kind, reparentTargetKind, parentTargetKind, operations, out errorMessage);

            default:
                errorMessage = $"Unsupported edit action kind '{action.Kind}'.";
                return false;
        }
    }

    private static bool TryResolveTargetKind (
        string? targetLiteral,
        IpcEditTargetKind currentTargetKind,
        Dictionary<string, IpcEditTargetKind> aliases,
        out IpcEditTargetKind targetKind,
        out string errorMessage)
    {
        if (targetLiteral is null)
        {
            targetKind = currentTargetKind;
            errorMessage = string.Empty;
            return true;
        }

        if (!TryResolveAlias(targetLiteral, aliases, out targetKind, out errorMessage))
        {
            return false;
        }

        return true;
    }

    private static bool TryResolveAlias (
        string aliasReference,
        Dictionary<string, IpcEditTargetKind> aliases,
        out IpcEditTargetKind targetKind,
        out string errorMessage)
    {
        targetKind = default;
        errorMessage = string.Empty;
        if (!IsAliasReference(aliasReference))
        {
            errorMessage = "Edit action target must reference a local binding using '$name'.";
            return false;
        }

        var aliasName = aliasReference[1..];
        if (!aliases.TryGetValue(aliasName, out targetKind))
        {
            errorMessage = $"Edit action target binding was not found: {aliasReference}.";
            return false;
        }

        return true;
    }

    private static bool IsAliasReference (string? literal)
    {
        return !string.IsNullOrWhiteSpace(literal)
               && literal!.Length > 1
               && literal[0] == '$';
    }

    private static void RegisterAlias (
        string? alias,
        IpcEditTargetKind targetKind,
        Dictionary<string, IpcEditTargetKind> aliases)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        aliases[alias] = targetKind;
    }

    private static bool TryAddOperationName (
        IpcEditStepContract.ContextKind contextKind,
        IpcEditStepContract.ActionKind actionKind,
        IpcEditTargetKind targetKind,
        IpcEditTargetKind? parentTargetKind,
        List<string> operations,
        out string errorMessage)
    {
        if (!IpcEditStepLoweringRules.TryGetActionOperationName(
                contextKind,
                actionKind,
                targetKind,
                parentTargetKind,
                out var operationName,
                out errorMessage))
        {
            return false;
        }

        operations.Add(operationName);
        return true;
    }
}