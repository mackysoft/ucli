using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Tests
{
    internal sealed class ManualLifecycleExecutionTimeSource : ILifecycleExecutionTimeSource
    {
        private readonly object gate = new();
        private readonly List<DeadlineRegistration> deadlineRegistrations = new();
        private readonly TaskCompletionSource<bool> deadlineReached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private DateTimeOffset utcNow;

        public ManualLifecycleExecutionTimeSource (DateTimeOffset utcNow)
        {
            if (utcNow.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Manual Lifecycle Execution time must use the UTC offset.",
                    nameof(utcNow));
            }

            this.utcNow = utcNow;
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                lock (gate)
                {
                    return utcNow;
                }
            }
        }

        public Task DeadlineReached => deadlineReached.Task;

        public ILifecycleExecutionDeadline CreateDeadlineNotification (
            DateTimeOffset deadlineUtc)
        {
            if (deadlineUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Lifecycle Execution deadline must use the UTC offset.",
                    nameof(deadlineUtc));
            }

            var registration = new DeadlineRegistration(this, deadlineUtc);
            lock (gate)
            {
                if (deadlineUtc <= utcNow)
                {
                    registration.Cancel();
                }
                else
                {
                    deadlineRegistrations.Add(registration);
                }
            }

            if (registration.IsCancellationRequested)
            {
                deadlineReached.TrySetResult(true);
            }

            return registration;
        }

        public void Advance (TimeSpan elapsedTime)
        {
            if (elapsedTime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedTime),
                    elapsedTime,
                    "Elapsed time must not be negative.");
            }

            List<DeadlineRegistration> dueRegistrations = null;
            lock (gate)
            {
                utcNow = utcNow.Add(elapsedTime);
                for (var index = deadlineRegistrations.Count - 1; index >= 0; index--)
                {
                    var registration = deadlineRegistrations[index];
                    if (registration.DeadlineUtc > utcNow)
                    {
                        continue;
                    }

                    dueRegistrations ??= new List<DeadlineRegistration>();
                    dueRegistrations.Add(registration);
                    deadlineRegistrations.RemoveAt(index);
                }
            }

            if (dueRegistrations == null)
            {
                return;
            }

            var deadlineWasReached = false;
            foreach (var registration in dueRegistrations)
            {
                deadlineWasReached |= registration.TryCancel();
            }
            if (deadlineWasReached)
            {
                deadlineReached.TrySetResult(true);
            }
        }

        private void Remove (DeadlineRegistration registration)
        {
            lock (gate)
            {
                deadlineRegistrations.Remove(registration);
            }
        }

        private sealed class DeadlineRegistration : ILifecycleExecutionDeadline
        {
            private readonly object gate = new();
            private readonly ManualLifecycleExecutionTimeSource owner;
            private readonly CancellationTokenSource source = new();
            private bool disposed;

            public DeadlineRegistration (
                ManualLifecycleExecutionTimeSource owner,
                DateTimeOffset deadlineUtc)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                DeadlineUtc = deadlineUtc;
            }

            public DateTimeOffset DeadlineUtc { get; }

            public CancellationToken Token => source.Token;

            public bool IsCancellationRequested => source.IsCancellationRequested;

            public bool TryCancel ()
            {
                lock (gate)
                {
                    if (disposed || source.IsCancellationRequested)
                    {
                        return false;
                    }

                    source.Cancel();
                    return true;
                }
            }

            public void Cancel ()
            {
                _ = TryCancel();
            }

            public void Dispose ()
            {
                owner.Remove(this);
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    source.Dispose();
                }
            }
        }
    }
}
