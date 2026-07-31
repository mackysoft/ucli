using System;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Retains refresh evidence interpreted only by the refresh handler. </summary>
    internal sealed record RefreshLifecycleExecutionCheckpoint
    {
        public const int CurrentSchemaVersion = 2;

        [JsonConstructor]
        public RefreshLifecycleExecutionCheckpoint (
            int schemaVersion,
            Guid executionId,
            UnityEditorObservation before,
            RefreshLifecycleDispatchCandidate dispatchCandidate,
            bool sideEffectAdmitted,
            bool providerInvocationObserved,
            bool providerReturned)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    schemaVersion,
                    "Unsupported refresh checkpoint schema version.");
            }
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Refresh execution identifier must not be empty.",
                    nameof(executionId));
            }
            if (sideEffectAdmitted && before == null)
            {
                throw new ArgumentException(
                    "An admitted refresh side effect requires durable before evidence.",
                    nameof(sideEffectAdmitted));
            }
            if (dispatchCandidate != null && !sideEffectAdmitted)
            {
                throw new ArgumentException(
                    "A refresh dispatch candidate requires prior side-effect admission.",
                    nameof(dispatchCandidate));
            }
            if (providerInvocationObserved
                && (!sideEffectAdmitted || dispatchCandidate == null))
            {
                throw new ArgumentException(
                    "An observed refresh provider invocation requires admission and a durable dispatch candidate.",
                    nameof(providerInvocationObserved));
            }
            if (providerReturned && !providerInvocationObserved)
            {
                throw new ArgumentException(
                    "A returned refresh provider call requires an observed invocation.",
                    nameof(providerReturned));
            }

            SchemaVersion = schemaVersion;
            ExecutionId = executionId;
            Before = before;
            DispatchCandidate = dispatchCandidate;
            SideEffectAdmitted = sideEffectAdmitted;
            ProviderInvocationObserved = providerInvocationObserved;
            ProviderReturned = providerReturned;
        }

        public int SchemaVersion { get; }

        public Guid ExecutionId { get; }

        public UnityEditorObservation Before { get; }

        public RefreshLifecycleDispatchCandidate DispatchCandidate { get; }

        public bool SideEffectAdmitted { get; }

        public bool ProviderInvocationObserved { get; }

        public bool ProviderReturned { get; }
    }

    /// <summary>
    /// Retains the durable call-boundary values that may become public start
    /// evidence only after the provider invocation is observed.
    /// </summary>
    internal sealed record RefreshLifecycleDispatchCandidate
    {
        [JsonConstructor]
        public RefreshLifecycleDispatchCandidate (
            DateTimeOffset startedAtUtc,
            long? domainReloadGenerationBefore)
        {
            if (startedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Refresh dispatch time must use the UTC offset.",
                    nameof(startedAtUtc));
            }
            if (domainReloadGenerationBefore is < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(domainReloadGenerationBefore),
                    domainReloadGenerationBefore,
                    "Refresh dispatch generation must not be negative.");
            }

            StartedAtUtc = startedAtUtc;
            DomainReloadGenerationBefore = domainReloadGenerationBefore;
        }

        public DateTimeOffset StartedAtUtc { get; }

        public long? DomainReloadGenerationBefore { get; }
    }
}
