using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Provides an immediately completable mutation boundary for handler-level tests. </summary>
    internal sealed class ImmediateUnityMutationLaneControl : IUnityMutationLaneControl
    {
        private Task quarantineCompletion = Task.CompletedTask;

        public bool IsBusy => IsQuarantined;

        public bool HasUnfinishedWork => !quarantineCompletion.IsCompleted;

        public bool IsQuarantined { get; private set; }

        public int BeginCount { get; private set; }

        public int CompleteCount { get; private set; }

        public IUnityMutationActivity BeginMutation ()
        {
            BeginCount++;
            return new Activity(this);
        }

        public void Quarantine (string reason, Task mutationCompletion)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Quarantine reason must not be empty.", nameof(reason));
            }

            quarantineCompletion = mutationCompletion ?? throw new ArgumentNullException(nameof(mutationCompletion));
            IsQuarantined = true;
        }

        public bool TrySealAdmissionForRetirement (out IDisposable admissionSeal)
        {
            admissionSeal = NoOpAdmissionSeal.Instance;
            return !HasUnfinishedWork || IsQuarantined;
        }

        public Task WaitForRetirementAsync ()
        {
            return quarantineCompletion;
        }

        private sealed class Activity : IUnityMutationActivity
        {
            private readonly ImmediateUnityMutationLaneControl owner;

            private int completed;

            public Activity (ImmediateUnityMutationLaneControl owner)
            {
                this.owner = owner;
            }

            public void Complete ()
            {
                if (Interlocked.Exchange(ref completed, 1) == 0)
                {
                    owner.CompleteCount++;
                }
            }
        }

        private sealed class NoOpAdmissionSeal : IDisposable
        {
            public static readonly NoOpAdmissionSeal Instance = new NoOpAdmissionSeal();

            public void Dispose ()
            {
            }
        }
    }
}
