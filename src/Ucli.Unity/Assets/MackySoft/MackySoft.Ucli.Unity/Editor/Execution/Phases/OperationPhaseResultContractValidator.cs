using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Validates observed phase results against declared operation contract metadata. </summary>
    internal static class OperationPhaseResultContractValidator
    {
        /// <summary> Creates runtime contract violation entries for one observed phase result. </summary>
        public static IReadOnlyList<OperationContractViolation> Validate (
            NormalizedOperation operation,
            UcliOperationMetadata metadata,
            OperationPhaseStepResult result)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var violations = new List<OperationContractViolation>();
            if (metadata.Kind == UcliOperationKind.Query)
            {
                AddQueryViolations(operation, result, violations);
                return violations;
            }

            var assurance = metadata.DescribeContract.Assurance;
            if (!assurance.MayDirty && result.Changed)
            {
                violations.Add(CreateViolation(
                    operation,
                    result,
                    "assurance.mayDirty=false",
                    "opResults[].changed=true"));
            }

            var allowedTouchedKinds = new HashSet<string>(assurance.TouchedKinds, StringComparer.Ordinal);
            for (var i = 0; i < result.Touched.Count; i++)
            {
                var kindName = ToTouchedKindName(result.Touched[i].Kind);
                if (allowedTouchedKinds.Contains(kindName))
                {
                    continue;
                }

                violations.Add(CreateViolation(
                    operation,
                    result,
                    $"assurance.touchedKinds contains '{kindName}'",
                    $"opResults[].touched[].kind={kindName}"));
            }

            return violations;
        }

        private static void AddQueryViolations (
            NormalizedOperation operation,
            OperationPhaseStepResult result,
            List<OperationContractViolation> violations)
        {
            if (result.Applied)
            {
                violations.Add(CreateViolation(
                    operation,
                    result,
                    "operation.kind=query",
                    "opResults[].applied=true"));
            }

            if (result.Changed)
            {
                violations.Add(CreateViolation(
                    operation,
                    result,
                    "operation.kind=query",
                    "opResults[].changed=true"));
            }

            if (result.Touched.Count != 0)
            {
                violations.Add(CreateViolation(
                    operation,
                    result,
                    "operation.kind=query",
                    "opResults[].touched is non-empty"));
            }
        }

        private static OperationContractViolation CreateViolation (
            NormalizedOperation operation,
            OperationPhaseStepResult result,
            string expectedFact,
            string observedResult)
        {
            return new OperationContractViolation(
                OpId: operation.Id,
                Operation: operation.Op,
                ExpectedFact: expectedFact,
                ObservedResult: observedResult,
                ApplicationState: ResolveApplicationState(result));
        }

        private static string ResolveApplicationState (OperationPhaseStepResult result)
        {
            if (result.Applied)
            {
                return OperationContractViolationApplicationStateNames.Applied;
            }

            return result.Changed || result.Touched.Count != 0
                ? OperationContractViolationApplicationStateNames.Indeterminate
                : OperationContractViolationApplicationStateNames.NotApplied;
        }

        private static string ToTouchedKindName (OperationTouchKind kind)
        {
            switch (kind)
            {
                case OperationTouchKind.Scene:
                    return "scene";

                case OperationTouchKind.Prefab:
                    return "prefab";

                case OperationTouchKind.Asset:
                    return "asset";

                case OperationTouchKind.ProjectSettings:
                    return "projectSettings";

                default:
                    return kind.ToString();
            }
        }
    }
}
