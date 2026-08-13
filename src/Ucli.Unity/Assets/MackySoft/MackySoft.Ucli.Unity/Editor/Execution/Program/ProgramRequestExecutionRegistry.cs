using System;
using System.Collections.Generic;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Execution.Program
{
    /// <summary> Retains a Program-owned logical Request through its fixed deadline and finite result-recovery interval. </summary>
    internal sealed class ProgramRequestExecutionRegistry
    {
        internal static readonly TimeSpan DefaultRecoveryUpperBound = TimeSpan.FromMinutes(1);

        private readonly object syncRoot = new object();
        private readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        private readonly IMonotonicClock clock;
        private readonly Func<DateTimeOffset> utcNow;
        private readonly TimeSpan recoveryUpperBound;

        public ProgramRequestExecutionRegistry (IMonotonicClock clock)
            : this(clock, static () => DateTimeOffset.UtcNow, DefaultRecoveryUpperBound) { }

        internal ProgramRequestExecutionRegistry (
            IMonotonicClock clock,
            Func<DateTimeOffset> utcNow,
            TimeSpan recoveryUpperBound)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            if (recoveryUpperBound <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(recoveryUpperBound));
            this.recoveryUpperBound = recoveryUpperBound;
        }

        public ProgramRequestExecutionRegistration AcquireStart (
            Guid executionId,
            IpcProgramRequestExecutionBinding binding,
            CancellationTokenSource cancellationSource)
        {
            return Acquire(executionId, binding, attachOnly: false, cancellationSource ?? throw new ArgumentNullException(nameof(cancellationSource)));
        }

        public ProgramRequestExecutionRegistration Attach (Guid executionId, IpcProgramRequestExecutionBinding binding)
        {
            return Acquire(executionId, binding, attachOnly: true, cancellationSource: null);
        }

        public ProgramRequestExecutionCancellationDisposition RequestCancellation (
            Guid executionId,
            IpcProgramRequestExecutionBinding binding,
            IpcProgramRequestCancellationReason reason)
        {
            lock (syncRoot)
            {
                RemoveExpired(clock.Elapsed);
                if (!entries.TryGetValue(executionId, out var entry)) return ProgramRequestExecutionCancellationDisposition.NotStarted;
                if (!HasSameBinding(entry.Binding, binding)) return ProgramRequestExecutionCancellationDisposition.Conflict;
                if (entry.ResponseBytes is not null) return ProgramRequestExecutionCancellationDisposition.Terminal;
                if (entry.State == State.Tombstone) return ProgramRequestExecutionCancellationDisposition.NotStarted;
                if (entry.CancellationSource is null) return ProgramRequestExecutionCancellationDisposition.Unsupported;

                entry.CancellationReason ??= reason;
                try
                {
                    entry.CancellationSource.Cancel();
                    return ProgramRequestExecutionCancellationDisposition.Requested;
                }
                catch (ObjectDisposedException)
                {
                    return entry.ResponseBytes is not null
                        ? ProgramRequestExecutionCancellationDisposition.Terminal
                        : ProgramRequestExecutionCancellationDisposition.Unsupported;
                }
            }
        }

        public bool TryGetTerminal (Guid executionId, IpcProgramRequestExecutionBinding binding, out byte[]? responseBytes)
        {
            lock (syncRoot)
            {
                RemoveExpired(clock.Elapsed);
                if (entries.TryGetValue(executionId, out var entry)
                    && HasSameBinding(entry.Binding, binding)
                    && entry.ResponseBytes is not null)
                {
                    responseBytes = (byte[])entry.ResponseBytes.Clone();
                    return true;
                }
            }

            responseBytes = null;
            return false;
        }

        public void Complete (Guid executionId, IpcProgramRequestExecutionBinding binding, byte[] responseBytes)
        {
            if (responseBytes is null) throw new ArgumentNullException(nameof(responseBytes));
            lock (syncRoot)
            {
                RemoveExpired(clock.Elapsed);
                if (!entries.TryGetValue(executionId, out var entry) || !HasSameBinding(entry.Binding, binding))
                {
                    throw new InvalidOperationException("Program Request terminal response has no matching logical execution.");
                }
                if (entry.State != State.Registered)
                {
                    throw new InvalidOperationException("Only an owned Program Request execution can publish a terminal response.");
                }
                if (entry.ResponseBytes is not null)
                {
                    throw new InvalidOperationException("Program Request terminal response was already published.");
                }

                entry.ResponseBytes = (byte[])responseBytes.Clone();
                entry.CancellationSource?.Dispose();
                entry.CancellationSource = null;
            }
        }

        public IpcProgramRequestCancellationReason? GetCancellationReason (
            Guid executionId,
            IpcProgramRequestExecutionBinding binding)
        {
            lock (syncRoot)
            {
                return entries.TryGetValue(executionId, out var entry) && HasSameBinding(entry.Binding, binding)
                    ? entry.CancellationReason
                    : null;
            }
        }

        private ProgramRequestExecutionRegistration Acquire (
            Guid executionId,
            IpcProgramRequestExecutionBinding binding,
            bool attachOnly,
            CancellationTokenSource? cancellationSource)
        {
            if (executionId == Guid.Empty) throw new ArgumentException("Program request execution id must not be empty.", nameof(executionId));
            if (binding is null) throw new ArgumentNullException(nameof(binding));

            lock (syncRoot)
            {
                var now = clock.Elapsed;
                RemoveExpired(now);
                if (binding.DeadlineUtc <= utcNow()) return ProgramRequestExecutionRegistration.Suppressed;
                if (entries.TryGetValue(executionId, out var existing))
                {
                    if (!HasSameBinding(existing.Binding, binding)) return ProgramRequestExecutionRegistration.Conflict;
                    return existing.State == State.Tombstone
                        ? ProgramRequestExecutionRegistration.Suppressed
                        : existing.ResponseBytes is null
                            ? ProgramRequestExecutionRegistration.Attached
                            : ProgramRequestExecutionRegistration.Terminal;
                }

                entries.Add(executionId, new Entry(
                    binding,
                    attachOnly ? State.Tombstone : State.Registered,
                    CreateExpiresAt(now, binding.DeadlineUtc),
                    cancellationSource));
                return attachOnly ? ProgramRequestExecutionRegistration.Suppressed : ProgramRequestExecutionRegistration.StartOwner;
            }
        }

        private TimeSpan CreateExpiresAt (TimeSpan now, DateTimeOffset deadlineUtc)
        {
            var remaining = deadlineUtc - utcNow();
            return now + (remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + recoveryUpperBound;
        }

        private void RemoveExpired (TimeSpan now)
        {
            var expired = new List<Guid>();
            foreach (var entry in entries)
            {
                if (entry.Value.ExpiresAt <= now) expired.Add(entry.Key);
            }
            foreach (var executionId in expired)
            {
                entries[executionId].CancellationSource?.Dispose();
                entries.Remove(executionId);
            }
        }

        private static bool HasSameBinding (IpcProgramRequestExecutionBinding left, IpcProgramRequestExecutionBinding right)
        {
            if (left.Project != right.Project
                || left.Host != right.Host
                || left.Generation != right.Generation
                || left.DeadlineUtc != right.DeadlineUtc
                || left.RequestDigest != right.RequestDigest
                || left.PlanDigest != right.PlanDigest
                || left.PlanTokenDigest != right.PlanTokenDigest
                || left.AuthorizationDigest != right.AuthorizationDigest
                || left.ConfigurationDigest != right.ConfigurationDigest
                || left.OperationDescriptorDigests.Count != right.OperationDescriptorDigests.Count)
            {
                return false;
            }

            for (var index = 0; index < left.OperationDescriptorDigests.Count; index++)
            {
                if (left.OperationDescriptorDigests[index] != right.OperationDescriptorDigests[index]) return false;
            }
            return true;
        }

        private enum State { Registered, Tombstone }

        private sealed class Entry
        {
            public Entry (
                IpcProgramRequestExecutionBinding binding,
                State state,
                TimeSpan expiresAt,
                CancellationTokenSource? cancellationSource)
            {
                Binding = binding;
                State = state;
                ExpiresAt = expiresAt;
                CancellationSource = cancellationSource;
            }

            public IpcProgramRequestExecutionBinding Binding { get; }
            public State State { get; }
            public TimeSpan ExpiresAt { get; }
            public CancellationTokenSource? CancellationSource { get; set; }
            public IpcProgramRequestCancellationReason? CancellationReason { get; set; }
            public byte[]? ResponseBytes { get; set; }
        }
    }

    internal enum ProgramRequestExecutionRegistration { StartOwner, Attached, Terminal, Suppressed, Conflict }

    internal enum ProgramRequestExecutionCancellationDisposition { Requested, Terminal, NotStarted, Conflict, Unsupported }
}
