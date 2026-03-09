using System;
using System.Collections.Generic;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.comp.set</c>. </summary>
    internal static class CompSetArgumentsCodec
    {
        public static bool TryParse (
            JsonElement args,
            out CompSetArguments parsedArguments,
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
            List<CompSetAssignment>? assignments = null;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, CompOperationPropertyNames.Target, StringComparison.Ordinal))
                {
                    if (hasTarget)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {CompOperationPropertyNames.Target}.";
                        return false;
                    }

                    if (!UnityObjectReferenceCodec.TryParse(property.Value, "args.target", out targetReference, out errorMessage))
                    {
                        return false;
                    }

                    hasTarget = true;
                    continue;
                }

                if (string.Equals(property.Name, CompOperationPropertyNames.Sets, StringComparison.Ordinal))
                {
                    if (hasSets)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {CompOperationPropertyNames.Sets}.";
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
                errorMessage = $"Operation 'args' requires property '{CompOperationPropertyNames.Target}'.";
                return false;
            }

            if (!hasSets)
            {
                errorMessage = $"Operation 'args' requires property '{CompOperationPropertyNames.Sets}'.";
                return false;
            }

            parsedArguments = new CompSetArguments(targetReference, assignments!);
            return true;
        }

        private static bool TryParseAssignments (
            JsonElement element,
            out List<CompSetAssignment>? assignments,
            out string errorMessage)
        {
            assignments = null;
            errorMessage = string.Empty;
            if (element.ValueKind != JsonValueKind.Array)
            {
                errorMessage = "Operation 'args.sets' must be an array.";
                return false;
            }

            assignments = new List<CompSetAssignment>();
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
                    if (string.Equals(property.Name, CompOperationPropertyNames.Path, StringComparison.Ordinal))
                    {
                        if (hasPath)
                        {
                            errorMessage = $"Operation 'args.sets[{index}]' contains duplicated property: {CompOperationPropertyNames.Path}.";
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

                    if (string.Equals(property.Name, CompOperationPropertyNames.Value, StringComparison.Ordinal))
                    {
                        if (hasValue)
                        {
                            errorMessage = $"Operation 'args.sets[{index}]' contains duplicated property: {CompOperationPropertyNames.Value}.";
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
                    errorMessage = $"Operation 'args.sets[{index}]' requires property '{CompOperationPropertyNames.Path}'.";
                    return false;
                }

                if (!hasValue)
                {
                    errorMessage = $"Operation 'args.sets[{index}]' requires property '{CompOperationPropertyNames.Value}'.";
                    return false;
                }

                assignments.Add(new CompSetAssignment(path!, value));
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