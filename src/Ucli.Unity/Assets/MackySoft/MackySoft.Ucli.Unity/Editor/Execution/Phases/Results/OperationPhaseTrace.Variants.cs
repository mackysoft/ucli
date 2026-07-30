using System;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    internal sealed partial record OperationPhaseTrace
    {
        /// <summary> Creates the valid final trace variants emitted by phase execution. </summary>
        internal static class Variants
        {
        public static OperationPhaseTrace SkippedBeforeContractResolution (
            IpcExecuteStepId opId,
            string op) =>
            CreateSkipped(opId, op, contracts: null);

        public static OperationPhaseTrace SkippedAgainstContract (
            IpcExecuteStepId opId,
            string op,
            OperationContractFacts contracts) =>
            CreateSkipped(opId, op, RequireContracts(contracts));

        public static OperationPhaseTrace ValidationFailureBeforeContractResolution (
            IpcExecuteStepId opId,
            string op,
            OperationFailure failure)
        {
            return new OperationPhaseTrace(
                opId,
                op,
                OperationPhase.Validate,
                OperationPhaseStepResult.Failed(
                    failure,
                    applied: false,
                    changed: false,
                    result: null,
                    Array.Empty<OperationTouch>()),
                contracts: null);
        }

        public static OperationPhaseTrace ValidationFailure (
            IpcExecuteStepId opId,
            string op,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts) =>
            CreateFailure(opId, op, OperationPhase.Validate, outcome, contracts);

        public static OperationPhaseTrace PlanFailure (
            IpcExecuteStepId opId,
            string op,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts) =>
            CreateFailure(opId, op, OperationPhase.Plan, outcome, contracts);

        public static OperationPhaseTrace CallFailure (
            IpcExecuteStepId opId,
            string op,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts) =>
            CreateFailure(opId, op, OperationPhase.Call, outcome, contracts);

        public static OperationPhaseTrace PlanSuccess (
            IpcExecuteStepId opId,
            string op,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts) =>
            CreateSuccessWithoutVerdict(
                opId,
                op,
                OperationPhase.Plan,
                outcome,
                contracts);

        public static OperationPhaseTrace CallSuccessWithoutVerdict (
            IpcExecuteStepId opId,
            string op,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts) =>
            CreateSuccessWithoutVerdict(
                opId,
                op,
                OperationPhase.Call,
                outcome,
                RequireNonJudgingContract(contracts));

        public static OperationPhaseTrace CallSuccessWithVerdict (
            IpcExecuteStepId opId,
            string op,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts)
        {
            var judgingContracts = RequireJudgingContract(contracts);
            return new OperationPhaseTrace(
                opId,
                op,
                OperationPhase.Call,
                RequireJudgingSuccess(outcome),
                judgingContracts);
        }

        private static OperationPhaseTrace CreateFailure (
            IpcExecuteStepId opId,
            string op,
            OperationPhase phase,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts)
        {
            if (outcome == null
                || outcome.IsSuccess
                || outcome.Verdict.HasValue
                || outcome.TypedResult != null)
            {
                throw new ArgumentException(
                    "A failure trace requires a failed finalized step result without a verdict.",
                    nameof(outcome));
            }

            return new OperationPhaseTrace(opId, op, phase, outcome, RequireContracts(contracts));
        }

        private static OperationPhaseTrace CreateSuccessWithoutVerdict (
            IpcExecuteStepId opId,
            string op,
            OperationPhase phase,
            OperationPhaseStepResult outcome,
            OperationContractFacts contracts)
        {
            if (outcome == null || !outcome.IsSuccess || outcome.Verdict.HasValue)
            {
                throw new ArgumentException(
                    "A non-judging success trace requires a successful step result without a verdict.",
                    nameof(outcome));
            }

            return new OperationPhaseTrace(
                opId,
                op,
                phase,
                outcome,
                RequireContracts(contracts));
        }

        private static OperationPhaseTrace CreateSkipped (
            IpcExecuteStepId opId,
            string op,
            OperationContractFacts? contracts) =>
            new OperationPhaseTrace(
                opId,
                op,
                OperationPhase.Skipped,
                OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false,
                    Array.Empty<OperationTouch>()),
                contracts);

        private static OperationPhaseStepResult RequireJudgingSuccess (
            OperationPhaseStepResult outcome)
        {
            if (outcome == null || !outcome.IsSuccess || !outcome.Verdict.HasValue)
            {
                throw new ArgumentException(
                    "A judging success trace requires a successful step result with a verdict.",
                    nameof(outcome));
            }
            if (!outcome.Result.HasValue)
            {
                throw new ArgumentException("A verdict requires serialized result evidence.", nameof(outcome));
            }

            return outcome;
        }

        private static OperationContractFacts RequireContracts (OperationContractFacts contracts) =>
            contracts ?? throw new ArgumentNullException(nameof(contracts));

        private static OperationContractFacts RequireNonJudgingContract (
            OperationContractFacts contracts)
        {
            RequireContracts(contracts);
            if (contracts.HasVerdictContract)
            {
                throw new ArgumentException(
                    "A non-judging Call success cannot use a verdict contract.",
                    nameof(contracts));
            }

            return contracts;
        }

        private static OperationContractFacts RequireJudgingContract (
            OperationContractFacts contracts)
        {
            RequireContracts(contracts);
            if (!contracts.HasVerdictContract)
            {
                throw new ArgumentException(
                    "A judging Call success requires a verdict contract.",
                    nameof(contracts));
            }

            return contracts;
        }
        }
    }
}
