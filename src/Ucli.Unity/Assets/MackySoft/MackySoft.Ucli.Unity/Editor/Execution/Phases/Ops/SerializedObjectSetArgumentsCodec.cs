using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates the shared argument contract for serialized-object set operations. </summary>
    internal static class SerializedObjectSetArgumentsCodec
    {
        private const string TargetPropertyName = "target";

        private const string SetsPropertyName = "sets";

        private const string PathPropertyName = "path";

        private const string ValuePropertyName = "value";

        public static bool TryParse (
            UcliOperationContracts.AssetSetArgs args,
            out SerializedObjectSetArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var targetReference, out errorMessage))
            {
                return false;
            }

            if (!TryParseAssignments(args.Sets, out var assignments, out errorMessage))
            {
                return false;
            }

            parsedArguments = new SerializedObjectSetArguments(targetReference, assignments!);
            return true;
        }

        public static bool TryParse (
            UcliOperationContracts.ComponentSetArgs args,
            out SerializedObjectSetArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var targetReference, out errorMessage))
            {
                return false;
            }

            if (!TryParseAssignments(args.Sets, out var assignments, out errorMessage))
            {
                return false;
            }

            parsedArguments = new SerializedObjectSetArguments(targetReference, assignments!);
            return true;
        }

        public static bool TryParse (
            JsonElement args,
            out SerializedObjectSetArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasTarget = false;
            var hasSets = false;
            var targetReference = default(UnityObjectReference);
            List<SerializedPropertyAssignment>? assignments = null;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, TargetPropertyName, StringComparison.Ordinal))
                {
                    if (hasTarget)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {TargetPropertyName}.";
                        return false;
                    }

                    if (!UnityObjectReferenceCodec.TryParse(property.Value, "args.target", out targetReference, out errorMessage))
                    {
                        return false;
                    }

                    hasTarget = true;
                    continue;
                }

                if (string.Equals(property.Name, SetsPropertyName, StringComparison.Ordinal))
                {
                    if (hasSets)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {SetsPropertyName}.";
                        return false;
                    }

                    if (!TryParseAssignments(property.Value, out assignments, out errorMessage))
                    {
                        return false;
                    }

                    hasSets = true;
                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (!hasTarget)
            {
                errorMessage = $"Operation 'args' requires property '{TargetPropertyName}'.";
                return false;
            }

            if (!hasSets)
            {
                errorMessage = $"Operation 'args' requires property '{SetsPropertyName}'.";
                return false;
            }

            parsedArguments = new SerializedObjectSetArguments(targetReference, assignments!);
            return true;
        }

        private static bool TryParseAssignments (
            IReadOnlyList<UcliOperationContracts.SerializedObjectSetItemArgs> sourceAssignments,
            out List<SerializedPropertyAssignment>? assignments,
            out string errorMessage)
        {
            assignments = null;
            errorMessage = string.Empty;
            if (sourceAssignments.Count == 0)
            {
                errorMessage = "Operation 'args.sets' must contain at least one assignment.";
                return false;
            }

            assignments = new List<SerializedPropertyAssignment>(sourceAssignments.Count);
            for (var i = 0; i < sourceAssignments.Count; i++)
            {
                var sourceAssignment = sourceAssignments[i];
                if (sourceAssignment.Value.ValueKind == JsonValueKind.Undefined)
                {
                    errorMessage = $"Operation 'args.sets[{i}]' requires property '{ValuePropertyName}'.";
                    return false;
                }

                assignments.Add(new SerializedPropertyAssignment(sourceAssignment.Path, sourceAssignment.Value.Clone()));
            }

            return true;
        }

        private static bool TryParseAssignments (
            JsonElement element,
            out List<SerializedPropertyAssignment>? assignments,
            out string errorMessage)
        {
            assignments = null;
            errorMessage = string.Empty;
            if (element.ValueKind != JsonValueKind.Array)
            {
                errorMessage = "Operation 'args.sets' must be an array.";
                return false;
            }

            assignments = new List<SerializedPropertyAssignment>();
            var index = 0;
            foreach (var assignmentElement in element.EnumerateArray())
            {
                if (assignmentElement.ValueKind != JsonValueKind.Object)
                {
                    errorMessage = $"Operation 'args.sets[{index}]' must be an object.";
                    return false;
                }

                var hasPath = false;
                var hasValue = false;
                string? path = null;
                var value = default(JsonElement);
                foreach (var property in assignmentElement.EnumerateObject())
                {
                    if (string.Equals(property.Name, PathPropertyName, StringComparison.Ordinal))
                    {
                        if (hasPath)
                        {
                            errorMessage = $"Operation 'args.sets[{index}]' contains duplicated property: {PathPropertyName}.";
                            return false;
                        }

                        if (!OperationArgumentValueReader.TryReadRequiredString(
                            property.Value,
                            $"args.sets[{index}].path",
                            expectedTypeDescription: "a string",
                            out path,
                            out errorMessage))
                        {
                            return false;
                        }

                        hasPath = true;
                        continue;
                    }

                    if (string.Equals(property.Name, ValuePropertyName, StringComparison.Ordinal))
                    {
                        if (hasValue)
                        {
                            errorMessage = $"Operation 'args.sets[{index}]' contains duplicated property: {ValuePropertyName}.";
                            return false;
                        }

                        value = property.Value.Clone();
                        hasValue = true;
                        continue;
                    }

                    errorMessage = $"Operation 'args.sets[{index}]' contains an unknown property: {property.Name}.";
                    return false;
                }

                if (!hasPath)
                {
                    errorMessage = $"Operation 'args.sets[{index}]' requires property '{PathPropertyName}'.";
                    return false;
                }

                if (!hasValue)
                {
                    errorMessage = $"Operation 'args.sets[{index}]' requires property '{ValuePropertyName}'.";
                    return false;
                }

                assignments.Add(new SerializedPropertyAssignment(path!, value));
                index++;
            }

            if (assignments.Count == 0)
            {
                errorMessage = "Operation 'args.sets' must contain at least one assignment.";
                return false;
            }

            return true;
        }
    }
}
