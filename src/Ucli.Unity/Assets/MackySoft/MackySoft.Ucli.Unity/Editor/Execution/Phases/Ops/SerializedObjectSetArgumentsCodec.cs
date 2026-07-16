using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates the shared argument contract for serialized-object set operations. </summary>
    internal static class SerializedObjectSetArgumentsCodec
    {
        public static bool TryParse (
            AssetSetArgs args,
            OperationAliasReferenceMap aliasReferences,
            out SerializedObjectSetArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            if (!UnityObjectReferenceContractMapper.TryMap(
                    args.Target,
                    "args.target",
                    aliasReferences,
                    out var targetReference,
                    out errorMessage))
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
            ComponentSetArgs args,
            OperationAliasReferenceMap aliasReferences,
            out SerializedObjectSetArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            if (!UnityObjectReferenceContractMapper.TryMap(
                    args.Target,
                    "args.target",
                    aliasReferences,
                    out var targetReference,
                    out errorMessage))
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

        private static bool TryParseAssignments (
            IReadOnlyList<SerializedObjectSetItemArgs> sourceAssignments,
            out List<SerializedPropertyAssignment>? assignments,
            out string errorMessage)
        {
            assignments = null;
            errorMessage = string.Empty;
            assignments = new List<SerializedPropertyAssignment>(sourceAssignments.Count);
            for (var i = 0; i < sourceAssignments.Count; i++)
            {
                var sourceAssignment = sourceAssignments[i];
                if (sourceAssignment == null)
                {
                    errorMessage = $"Operation 'args.sets[{i}]' must be an object.";
                    return false;
                }

                assignments.Add(new SerializedPropertyAssignment(
                    sourceAssignment.Path,
                    sourceAssignment.Value.Clone()));
            }

            return true;
        }

    }
}
