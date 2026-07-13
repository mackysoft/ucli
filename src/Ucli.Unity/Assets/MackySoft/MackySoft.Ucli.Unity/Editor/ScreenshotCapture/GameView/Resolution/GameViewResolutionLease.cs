using System;
using System.Collections.Generic;
using MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView.Resolution
{
    /// <summary> Owns one temporary GameView resolution and restores it on every exit path. </summary>
    internal sealed class GameViewResolutionLease
    {
        internal delegate bool TryAcquirePresentationRecoveryOwnership (out string errorMessage);

        internal enum RestoreOutcome
        {
            Retryable,
            RestoredOriginal,
            UserSelectionPreserved,
            OwnershipHandedOff,
        }

        private const int DeferredRecoveryUpdateInterval = 30;

        private static readonly Dictionary<string, GameViewResolutionLease> ActiveLeases =
            new(StringComparer.Ordinal);

        private readonly EditorWindow gameView;

        private readonly UnityGameViewResolutionAdapter.ResolutionGroup group;

        private readonly OriginalResolutionState original;

        private readonly TemporaryResolutionState temporary;

        private bool isRestoring;

        private bool activationSucceeded;

        private bool cleanupComplete;

        private bool restoreSucceeded;

        private bool selectionRestoredByLease;

        private bool temporaryRemovedByLease;

        private bool selectionConflictObserved;

        private bool deferredRecoveryScheduled;

        private bool recoveryHandedOff;

        private TryAcquirePresentationRecoveryOwnership deferredPresentationRecoveryOwnership;

        private int deferredRecoveryUpdateCountdown;

        private string restoreErrorMessage;

        public GameViewResolutionLease (
            EditorWindow gameView,
            UnityGameViewResolutionAdapter.ResolutionGroup group,
            OriginalResolutionState original,
            TemporaryResolutionState temporary)
        {
            if (gameView == null)
            {
                throw new ArgumentNullException(nameof(gameView));
            }

            this.gameView = gameView;
            this.group = group ?? throw new ArgumentNullException(nameof(group));
            this.original = original ?? throw new ArgumentNullException(nameof(original));
            this.temporary = temporary ?? throw new ArgumentNullException(nameof(temporary));
            if (ActiveLeases.ContainsKey(temporary.Label))
            {
                throw new InvalidOperationException(
                    $"GameView resolution ownership label is already active: {temporary.Label}");
            }

            ActiveLeases.Add(temporary.Label, this);
            AssemblyReloadEvents.beforeAssemblyReload += RestoreBestEffort;
            EditorApplication.quitting += RestoreBestEffort;
        }

        public EditorWindow GameView => gameView;

        /// <summary> Indicates whether selection and temporary-entry cleanup can still be retried. </summary>
        public bool CanRetryRestore => !cleanupComplete && !restoreSucceeded && !recoveryHandedOff;

        internal bool IsDeferredRecoveryScheduled => deferredRecoveryScheduled;

        internal static bool HasActiveOwnership => ActiveLeases.Count != 0;

        /// <summary> Retains failed activation ownership and retries recovery on subsequent Editor updates. </summary>
        internal void ScheduleDeferredRecovery (
            TryAcquirePresentationRecoveryOwnership tryAcquirePresentationRecoveryOwnership)
        {
            if (tryAcquirePresentationRecoveryOwnership == null)
            {
                throw new ArgumentNullException(nameof(tryAcquirePresentationRecoveryOwnership));
            }

            if (!CanRetryRestore || deferredRecoveryScheduled)
            {
                return;
            }

            deferredPresentationRecoveryOwnership = tryAcquirePresentationRecoveryOwnership;
            deferredRecoveryScheduled = true;
            EditorApplication.update += RetryDeferredRecovery;
        }

        /// <summary> Runs one internally owned recovery attempt or hands durable ownership to orphan cleanup. </summary>
        internal void RetryDeferredRecovery ()
        {
            if (!deferredRecoveryScheduled)
            {
                return;
            }

            if (gameView == null)
            {
                HandOffToOrphanRecovery(
                    "The target GameView was destroyed before deferred resolution recovery.");
                return;
            }

            if (deferredRecoveryUpdateCountdown > 0)
            {
                deferredRecoveryUpdateCountdown--;
                return;
            }

            deferredRecoveryUpdateCountdown = DeferredRecoveryUpdateInterval - 1;
            if (!HasOwnershipMarker())
            {
                cleanupComplete = true;
                deferredPresentationRecoveryOwnership = null;
                Unsubscribe();
                return;
            }

            var outcome = TryRestore(deferredPresentationRecoveryOwnership, out _);
            if (outcome != RestoreOutcome.Retryable)
            {
                deferredPresentationRecoveryOwnership = null;
            }
        }

        /// <summary> Selects the request-owned resolution and transfers mutation ownership to this lease. </summary>
        public bool TryActivate (out string errorMessage)
        {
            if (activationSucceeded)
            {
                errorMessage = null;
                return true;
            }

            if (cleanupComplete || restoreSucceeded)
            {
                errorMessage = "GameView resolution lease is no longer available for activation.";
                return false;
            }

            try
            {
                if (!group.IsCurrent)
                {
                    errorMessage =
                        "Current GameView size group changed before the temporary resolution was selected.";
                    return false;
                }

                if (!group.TryValidateExclusiveGameView(gameView, out errorMessage))
                {
                    return false;
                }

                if (!TryInspectTemporaryState(
                    out var temporaryPresent,
                    out errorMessage)
                    || !temporaryPresent)
                {
                    errorMessage ??=
                        "The request-owned GameView resolution is unavailable for selection.";
                    return false;
                }

                group.Select(gameView, temporary.Index);
                if (!group.IsCurrent)
                {
                    errorMessage =
                        "Current GameView size group changed while the temporary resolution was selected.";
                    return false;
                }

                if (!group.TryValidateExclusiveGameView(gameView, out errorMessage))
                {
                    return false;
                }

                if (!TryInspectTemporaryState(
                    out temporaryPresent,
                    out errorMessage)
                    || !temporaryPresent
                    || !group.TryGetSelectedIndex(gameView, out var selectedIndex)
                    || selectedIndex != temporary.Index)
                {
                    errorMessage ??=
                        "Unity did not select the request-owned GameView resolution exactly.";
                    return false;
                }

                gameView.Repaint();
                activationSucceeded = true;
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"GameView resolution activation failed. {UnityEditorReflection.UnwrapInvocationException(exception).Message}";
                return false;
            }
        }

        public RestoreOutcome TryRestore (
            TryAcquirePresentationRecoveryOwnership tryAcquirePresentationRecoveryOwnership,
            out string errorMessage)
        {
            if (tryAcquirePresentationRecoveryOwnership == null)
            {
                throw new ArgumentNullException(nameof(tryAcquirePresentationRecoveryOwnership));
            }

            if (restoreSucceeded)
            {
                errorMessage = restoreErrorMessage;
                return RestoreOutcome.RestoredOriginal;
            }

            if (cleanupComplete && selectionConflictObserved)
            {
                errorMessage = restoreErrorMessage;
                return RestoreOutcome.UserSelectionPreserved;
            }

            if (cleanupComplete || recoveryHandedOff)
            {
                errorMessage = restoreErrorMessage;
                return RestoreOutcome.OwnershipHandedOff;
            }

            if (isRestoring)
            {
                errorMessage = "GameView resolution restoration is already running.";
                return RestoreOutcome.Retryable;
            }

            isRestoring = true;
            try
            {
                if (!group.TryValidateExclusiveGameView(gameView, out restoreErrorMessage))
                {
                    errorMessage = restoreErrorMessage;
                    return RestoreOutcome.Retryable;
                }

                if (!group.IsCurrent)
                {
                    restoreErrorMessage =
                        "Current GameView size group changed during capture; no selection or collection was modified.";
                    errorMessage = restoreErrorMessage;
                    return RestoreOutcome.Retryable;
                }

                if (!TryInspectTemporaryState(
                    out var temporaryPresent,
                    out restoreErrorMessage))
                {
                    errorMessage = restoreErrorMessage;
                    return RestoreOutcome.Retryable;
                }

                if (temporaryPresent)
                {
                    if (!group.TryGetSelectedIndex(gameView, out var selectedIndex))
                    {
                        restoreErrorMessage = "Current GameView resolution selection is unavailable.";
                        errorMessage = restoreErrorMessage;
                        return RestoreOutcome.Retryable;
                    }

                    if (selectionConflictObserved)
                    {
                        if (selectedIndex == temporary.Index)
                        {
                            restoreErrorMessage =
                                "GameView resolution selection changed again after a restoration conflict; no selection or collection was modified.";
                            errorMessage = restoreErrorMessage;
                            return RestoreOutcome.Retryable;
                        }

                        return TryCompleteSelectionConflictCleanup(out errorMessage);
                    }

                    if (!selectionRestoredByLease)
                    {
                        if (selectedIndex != temporary.Index)
                        {
                            return TryCompleteSelectionConflictCleanup(out errorMessage);
                        }

                        group.Select(gameView, original.Index);
                        if (!group.TryGetSelectedIndex(gameView, out var restoredIndex)
                            || restoredIndex != original.Index)
                        {
                            restoreErrorMessage =
                                "GameView did not apply the request-owned selection restoration exactly.";
                            errorMessage = restoreErrorMessage;
                            return RestoreOutcome.Retryable;
                        }

                        selectionRestoredByLease = true;
                    }
                    else if (selectedIndex != original.Index)
                    {
                        restoreErrorMessage =
                            "GameView resolution selection changed after request-owned restoration; no further selection was applied.";
                        errorMessage = restoreErrorMessage;
                        return RestoreOutcome.Retryable;
                    }
                }
                else if (selectionConflictObserved && temporaryRemovedByLease)
                {
                    return TryFinalizeSelectionConflictCleanup(out errorMessage);
                }
                else if (!selectionRestoredByLease || !temporaryRemovedByLease)
                {
                    restoreErrorMessage =
                        "Temporary GameView resolution state changed outside the request-owned restoration path.";
                    errorMessage = restoreErrorMessage;
                    return RestoreOutcome.Retryable;
                }

                if (!TryRemoveOwnedTemporary(out restoreErrorMessage)
                    || !TryValidateRestoredStateCore(out restoreErrorMessage))
                {
                    errorMessage = restoreErrorMessage;
                    return RestoreOutcome.Retryable;
                }

                if (!tryAcquirePresentationRecoveryOwnership(out var ownershipError))
                {
                    restoreErrorMessage =
                        $"GameView resolution state was restored, but presentation recovery ownership could not be retained. {ownershipError}";
                    errorMessage = restoreErrorMessage;
                    return RestoreOutcome.Retryable;
                }

                if (!TryClearOwnershipMarker(out restoreErrorMessage))
                {
                    errorMessage = restoreErrorMessage;
                    return RestoreOutcome.Retryable;
                }

                restoreSucceeded = true;
                cleanupComplete = true;
                restoreErrorMessage = null;
                Unsubscribe();
                errorMessage = null;
                return RestoreOutcome.RestoredOriginal;
            }
            catch (Exception exception)
            {
                restoreErrorMessage = $"GameView resolution restoration failed. {exception.Message}";
                errorMessage = restoreErrorMessage;
                return RestoreOutcome.Retryable;
            }
            finally
            {
                isRestoring = false;
            }
        }

        public bool TryValidateRestoredState (out string errorMessage)
        {
            if (!restoreSucceeded)
            {
                errorMessage = restoreErrorMessage ?? "GameView resolution restoration has not run.";
                return false;
            }

            try
            {
                return TryValidateRestoredStateCore(out errorMessage);
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"GameView resolution restoration could not be verified. {exception.Message}";
                return false;
            }
        }

        /// <summary> Determines whether restoring the original presentation remains applicable after an external change. </summary>
        public bool IsOriginalPresentationRecoveryApplicable ()
        {
            try
            {
                return gameView != null
                    && group.IsCurrent
                    && group.TryGetSelectedIndex(gameView, out var selectedIndex)
                    && selectedIndex == original.Index
                    && original.Index >= 0
                    && original.Index < group.GetTotalCount()
                    && ReferenceEquals(group.GetSize(original.Index), original.Size);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryRemoveOwnedTemporary (out string errorMessage)
        {
            if (!group.TryValidateExclusiveGameView(gameView, out errorMessage))
            {
                return false;
            }

            var count = group.GetTotalCount();
            if (count == original.Count + 1)
            {
                if (temporary.Index < 0
                    || temporary.Index >= count
                    || !ReferenceEquals(group.GetSize(temporary.Index), temporary.Size))
                {
                    errorMessage =
                        "Temporary GameView resolution identity changed before restoration; no unrelated entry was removed.";
                    return false;
                }

                group.Remove(temporary.Index);
                temporaryRemovedByLease = true;
            }
            else if (count != original.Count)
            {
                errorMessage =
                    "GameView resolution collection changed unexpectedly; no unrelated entry was removed.";
                return false;
            }
            else if (!temporaryRemovedByLease)
            {
                errorMessage =
                    "Temporary GameView resolution disappeared outside the request-owned restoration path.";
                return false;
            }

            return TryValidateOriginalGroup(out errorMessage);
        }

        private bool TryInspectTemporaryState (
            out bool temporaryPresent,
            out string errorMessage)
        {
            temporaryPresent = false;
            var count = group.GetTotalCount();
            if (original.Index < 0
                || original.Index >= count
                || !ReferenceEquals(group.GetSize(original.Index), original.Size))
            {
                errorMessage =
                    "GameView resolution collection changed before request-owned restoration; no selection was modified.";
                return false;
            }

            if (count == original.Count + 1
                && temporary.Index >= 0
                && temporary.Index < count
                && ReferenceEquals(group.GetSize(temporary.Index), temporary.Size))
            {
                temporaryPresent = true;
                errorMessage = null;
                return true;
            }

            if (count == original.Count)
            {
                for (var index = 0; index < count; index++)
                {
                    if (ReferenceEquals(group.GetSize(index), temporary.Size))
                    {
                        errorMessage =
                            "Temporary GameView resolution moved before request-owned restoration; no selection was modified.";
                        return false;
                    }
                }

                errorMessage = null;
                return true;
            }

            errorMessage =
                "GameView resolution collection changed before request-owned restoration; no selection was modified.";
            return false;
        }

        private RestoreOutcome TryCompleteSelectionConflictCleanup (out string errorMessage)
        {
            selectionConflictObserved = true;
            if (!TryRemoveOwnedTemporary(out restoreErrorMessage)
                || !TryValidateOriginalGroup(out restoreErrorMessage))
            {
                errorMessage = restoreErrorMessage;
                return RestoreOutcome.Retryable;
            }

            gameView.Repaint();
            return TryFinalizeSelectionConflictCleanup(out errorMessage);
        }

        private RestoreOutcome TryFinalizeSelectionConflictCleanup (out string errorMessage)
        {
            if (!TryClearOwnershipMarker(out restoreErrorMessage))
            {
                errorMessage = restoreErrorMessage;
                return RestoreOutcome.Retryable;
            }

            cleanupComplete = true;
            Unsubscribe();
            restoreErrorMessage =
                "GameView resolution selection changed during capture; the user selection was left untouched.";
            errorMessage = restoreErrorMessage;
            return RestoreOutcome.UserSelectionPreserved;
        }

        private bool TryValidateOriginalGroup (out string errorMessage)
        {
            var count = group.GetTotalCount();
            if (count != original.Count
                || original.Index < 0
                || original.Index >= count
                || !ReferenceEquals(group.GetSize(original.Index), original.Size))
            {
                errorMessage = "GameView resolution size collection was not restored exactly.";
                return false;
            }

            for (var index = 0; index < count; index++)
            {
                if (ReferenceEquals(group.GetSize(index), temporary.Size))
                {
                    errorMessage = "Temporary GameView resolution remains after restoration.";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        private bool TryValidateRestoredStateCore (out string errorMessage)
        {
            if (!group.IsCurrent
                || !group.TryGetSelectedIndex(gameView, out var selectedIndex)
                || selectedIndex != original.Index
                || !TryValidateOriginalGroup(out _))
            {
                errorMessage =
                    "GameView resolution selection or size collection was not restored exactly.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private bool TryClearOwnershipMarker (out string errorMessage)
        {
            return UnityScreenshotResolutionLeaseRegistry.TryUnregister(
                temporary.Label,
                out errorMessage);
        }

        private void Unsubscribe ()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= RestoreBestEffort;
            EditorApplication.quitting -= RestoreBestEffort;
            EditorApplication.update -= RetryDeferredRecovery;
            deferredRecoveryScheduled = false;
            if (ActiveLeases.TryGetValue(temporary.Label, out var active)
                && ReferenceEquals(active, this))
            {
                ActiveLeases.Remove(temporary.Label);
            }
        }

        private bool HasOwnershipMarker ()
        {
            if (!UnityScreenshotResolutionLeaseRegistry.TryRead(
                out var ownedResolutions,
                out _))
            {
                return true;
            }

            foreach (var ownedResolution in ownedResolutions)
            {
                if (string.Equals(
                    ownedResolution.Label,
                    temporary.Label,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void HandOffToOrphanRecovery (string reason)
        {
            recoveryHandedOff = true;
            deferredPresentationRecoveryOwnership = null;
            restoreErrorMessage =
                $"{reason} The request-owned marker remains for fail-closed orphan cleanup.";
            Unsubscribe();
        }

        private void RestoreBestEffort ()
        {
            string errorMessage = null;
            for (var attempt = 0; attempt < 3 && CanRetryRestore; attempt++)
            {
                var outcome = TryRestore(AcceptSessionEndingRestore, out errorMessage);
                if (outcome != RestoreOutcome.Retryable)
                {
                    return;
                }
            }

            if (!restoreSucceeded)
            {
                Debug.LogError(
                    $"uCLI could not fully restore its temporary GameView resolution. {errorMessage}");
            }
        }

        private static bool AcceptSessionEndingRestore (out string errorMessage)
        {
            errorMessage = null;
            return true;
        }

        internal static void ClearActiveOwnershipForTests ()
        {
            foreach (var lease in new List<GameViewResolutionLease>(ActiveLeases.Values))
            {
                lease.Unsubscribe();
            }

            ActiveLeases.Clear();
        }

        internal sealed record OriginalResolutionState (
            int Index,
            int Count,
            object Size);

        internal sealed record TemporaryResolutionState (
            string Label,
            int Index,
            object Size);
    }
}
