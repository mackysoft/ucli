using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Implements <c>ucli.project.missingScripts.check</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class MissingScriptsCheckOperation : UcliOperation<MissingScriptsCheckArgs, MissingScriptsCheckResult>
    {
        private readonly IMissingScriptsScanEngine scanEngine;

        private readonly IUnityEditorReadinessGate readinessGate;

        private static readonly UcliOperationVerdictDefinition<MissingScriptsCheckResult> VerdictDefinition = new(
            "Returns fail when a missing script slot is confirmed, incomplete when any requested scope or discovered asset is unscanned, and pass otherwise.",
            EvaluateVerdict);

        public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.CreateJudgingQuery<MissingScriptsCheckArgs, MissingScriptsCheckResult>(
            operationName: UcliPrimitiveOperationNames.ProjectMissingScriptsCheck,
            description: "Checks saved scenes and prefabs under requested Assets directories for missing script component slots.",
            assurance: new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate requested Assets directories and inspect saved scene and prefab contents without applying mutation.",
                callSemantics: "Inspect saved scene and prefab contents and return confirmed missing script slots without applying mutation.",
                touchedContract: "Returns no touched resources because missing script inspection is observational.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Execution or cleanup failure returns the command execution envelope without an operation result or verdict; unscanned requested targets are represented in a successful incomplete result.",
                dangerousNotes: Array.Empty<string>()),
            verdict: VerdictDefinition,
            requiresPreCallPlanReplay: false,
            exposure: UcliOperationExposure.Public,
            playModeSupport: UcliOperationPlayModeSupport.Disallowed,
            codeContract: null);

        public MissingScriptsCheckOperation (
            IMissingScriptsScanEngine scanEngine,
            IUnityEditorReadinessGate readinessGate)
        {
            this.scanEngine = scanEngine ?? throw new ArgumentNullException(nameof(scanEngine));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
        }

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            MissingScriptsCheckArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(TryValidate(operation, args, cancellationToken, out var failure)
                ? OperationPhaseStepResult.Success(applied: false, changed: false, touched: Array.Empty<OperationTouch>())
                : failure!);
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            MissingScriptsCheckArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Execute(operation, args, cancellationToken));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            MissingScriptsCheckArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(operation, args, cancellationToken, out var failure))
            {
                return Task.FromResult(failure!);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var observation = readinessGate.CaptureObservation();
            cancellationToken.ThrowIfCancellationRequested();
            if (!observation.CanAcceptExecutionRequests)
            {
                return Task.FromResult(SuccessWithResult(
                    CreateEditorNotReadyResult(args, cancellationToken),
                    applied: false,
                    changed: false,
                    touched: Array.Empty<OperationTouch>()));
            }

            return Task.FromResult(ExecuteValidated(args, cancellationToken));
        }

        private OperationPhaseStepResult Execute (
            NormalizedOperation operation,
            MissingScriptsCheckArgs args,
            CancellationToken cancellationToken)
        {
            if (!TryValidate(operation, args, cancellationToken, out var failure))
            {
                return failure!;
            }

            return ExecuteValidated(args, cancellationToken);
        }

        private OperationPhaseStepResult ExecuteValidated (
            MissingScriptsCheckArgs args,
            CancellationToken cancellationToken)
        {
            return SuccessWithResult(
                scanEngine.Scan(args, cancellationToken),
                applied: false,
                changed: false,
                touched: Array.Empty<OperationTouch>());
        }

        private static MissingScriptsCheckResult CreateEditorNotReadyResult (
            MissingScriptsCheckArgs args,
            CancellationToken cancellationToken)
        {
            var unscannedScopes = new List<MissingScriptsUnscannedScope>(args.Roots.Count * args.AssetKinds.Count);
            for (var rootIndex = 0; rootIndex < args.Roots.Count; rootIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var assetKindIndex = 0; assetKindIndex < args.AssetKinds.Count; assetKindIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    unscannedScopes.Add(new MissingScriptsUnscannedScope(
                        args.Roots[rootIndex],
                        args.AssetKinds[assetKindIndex],
                        MissingScriptsUnscannedReason.EditorNotReady));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = new MissingScriptsCheckResult(
                new MissingScriptsRequestedScope(args.Roots, args.AssetKinds),
                unscannedScopes,
                Array.Empty<UnityAssetPath>(),
                Array.Empty<MissingScriptsUnscannedAsset>(),
                Array.Empty<MissingScriptSlot>());
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        private static bool TryValidate (
            NormalizedOperation operation,
            MissingScriptsCheckArgs args,
            CancellationToken cancellationToken,
            out OperationPhaseStepResult? failure)
        {
            for (var i = 0; i < args.Roots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isValidFolder = AssetDatabase.IsValidFolder(args.Roots[i].Value);
                cancellationToken.ThrowIfCancellationRequested();
                if (isValidFolder)
                {
                    continue;
                }

                failure = OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(
                    operation.Id,
                    $"Operation 'args.roots[{i}]' must identify an existing directory under Assets: {args.Roots[i].Value}.");
                return false;
            }

            failure = null;
            return true;
        }

        private static Verdict EvaluateVerdict (MissingScriptsCheckResult result)
        {
            if (result.MissingScriptSlots.Count > 0)
            {
                return Verdict.Fail;
            }

            return result.UnscannedScopes.Count > 0 || result.UnscannedAssets.Count > 0
                ? Verdict.Incomplete
                : Verdict.Pass;
        }
    }
}
