using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Reads optional prefab override property path filters for one edit action. </summary>
internal static class IpcEditStepPropertyPathsReader
{
    public static bool TryReadOptional (
        JsonElement actionElement,
        int actionIndex,
        out IReadOnlyList<string>? propertyPaths,
        out string errorMessage)
    {
        propertyPaths = null;
        errorMessage = string.Empty;
        if (!actionElement.TryGetProperty("propertyPaths", out var propertyPathsElement))
        {
            return true;
        }

        return TryRead(propertyPathsElement, actionIndex, out propertyPaths, out errorMessage);
    }

    private static bool TryRead (
        JsonElement propertyPathsElement,
        int actionIndex,
        out IReadOnlyList<string>? propertyPaths,
        out string errorMessage)
    {
        propertyPaths = null;
        if (propertyPathsElement.ValueKind != JsonValueKind.Array)
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].propertyPaths' must be an array.";
            return false;
        }

        return TryReadArray(propertyPathsElement, actionIndex, out propertyPaths, out errorMessage);
    }

    private static bool TryReadArray (
        JsonElement propertyPathsElement,
        int actionIndex,
        out IReadOnlyList<string>? propertyPaths,
        out string errorMessage)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var propertyPathIndex = 0;
        foreach (var propertyPathElement in propertyPathsElement.EnumerateArray())
        {
            if (!TryReadPath(propertyPathElement, actionIndex, propertyPathIndex, seen, out var propertyPath, out errorMessage))
            {
                propertyPaths = null;
                return false;
            }

            paths.Add(propertyPath);
            propertyPathIndex++;
        }

        return TryComplete(paths, actionIndex, out propertyPaths, out errorMessage);
    }

    private static bool TryReadPath (
        JsonElement propertyPathElement,
        int actionIndex,
        int propertyPathIndex,
        HashSet<string> seen,
        out string propertyPath,
        out string errorMessage)
    {
        propertyPath = string.Empty;
        if (propertyPathElement.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].propertyPaths[{propertyPathIndex}]' must be a string.";
            return false;
        }

        return TryValidatePath(propertyPathElement.GetString(), actionIndex, propertyPathIndex, seen, out propertyPath, out errorMessage);
    }

    private static bool TryValidatePath (
        string? candidate,
        int actionIndex,
        int propertyPathIndex,
        HashSet<string> seen,
        out string propertyPath,
        out string errorMessage)
    {
        propertyPath = candidate ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].propertyPaths[{propertyPathIndex}]' must not be empty.";
            return false;
        }

        if (!seen.Add(candidate))
        {
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].propertyPaths' contains duplicate path: {candidate}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryComplete (
        IReadOnlyList<string> paths,
        int actionIndex,
        out IReadOnlyList<string>? propertyPaths,
        out string errorMessage)
    {
        if (paths.Count == 0)
        {
            propertyPaths = null;
            errorMessage = $"Edit step property 'step.actions[{actionIndex}].propertyPaths' must contain at least one path when specified.";
            return false;
        }

        propertyPaths = paths;
        errorMessage = string.Empty;
        return true;
    }
}
