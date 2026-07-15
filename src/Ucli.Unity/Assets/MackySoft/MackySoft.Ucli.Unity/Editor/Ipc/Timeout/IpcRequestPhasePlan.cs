using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Defines monotonic cutoffs for one end-to-end IPC request exchange. </summary>
    internal sealed class IpcRequestPhasePlan
    {
        internal static readonly TimeSpan MaximumTimerDuration = TimeSpan.FromMilliseconds(int.MaxValue);

        private const int TerminalizationReserveDivisor = 10;

        private static readonly TimeSpan MinimumExecutionDuration = TimeSpan.FromMilliseconds(1);

        private static readonly TimeSpan MinimumTerminalizationReserve = TimeSpan.FromMilliseconds(150);

        private static readonly TimeSpan ResponseObservationMargin = TimeSpan.FromMilliseconds(10);

        private static readonly TimeSpan MaximumCompletionPersistenceDuration = TimeSpan.FromSeconds(1);

        private IpcRequestPhasePlan (
            TimeSpan executionCutoff,
            TimeSpan persistenceCutoff,
            TimeSpan writeCutoff)
        {
            ExecutionCutoff = executionCutoff;
            PersistenceCutoff = persistenceCutoff;
            WriteCutoff = writeCutoff;
        }

        /// <summary> Gets the elapsed duration at which authorization, queueing, and method execution stop. </summary>
        public TimeSpan ExecutionCutoff { get; }

        /// <summary> Gets the elapsed duration at which recoverable completion persistence stops. </summary>
        public TimeSpan PersistenceCutoff { get; }

        /// <summary> Gets the elapsed duration at which progress and terminal frame writes stop. </summary>
        public TimeSpan WriteCutoff { get; }

        /// <summary> Converts one UTC request deadline into ordered monotonic phase cutoffs. </summary>
        /// <param name="request"> The request envelope whose validated deadline defines the cutoffs. </param>
        /// <param name="observedAtUtc"> The UTC instant captured immediately after the request frame was read. </param>
        /// <param name="maximumResponseFrameWriteDuration"> The maximum duration reserved for the final response-write phase. </param>
        /// <returns> An immutable phase plan whose cutoffs are measured from creation. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when either timestamp does not use the UTC offset. </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="maximumResponseFrameWriteDuration" /> is not positive or exceeds the supported timer duration.
        /// </exception>
        public static IpcRequestPhasePlan Create (
            IpcRequestEnvelope request,
            DateTimeOffset observedAtUtc,
            TimeSpan maximumResponseFrameWriteDuration)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            EnsureUtc(observedAtUtc, nameof(observedAtUtc));
            if (maximumResponseFrameWriteDuration <= TimeSpan.Zero
                || maximumResponseFrameWriteDuration > MaximumTimerDuration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumResponseFrameWriteDuration),
                    maximumResponseFrameWriteDuration,
                    $"Maximum response frame write duration must be greater than zero and at most {MaximumTimerDuration.TotalMilliseconds:0} milliseconds.");
            }

            var observedRemaining = request.RequestDeadlineUtc - observedAtUtc;
            if (observedRemaining <= TimeSpan.Zero)
            {
                return new IpcRequestPhasePlan(
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero);
            }

            var remaining = GetShorter(
                observedRemaining,
                TimeSpan.FromMilliseconds(request.RequestDeadlineRemainingMilliseconds));
            if (remaining <= MinimumExecutionDuration)
            {
                return new IpcRequestPhasePlan(
                    remaining,
                    remaining,
                    remaining);
            }

            var observationMargin = GetShorter(
                ResponseObservationMargin,
                TimeSpan.FromTicks(remaining.Ticks / TerminalizationReserveDivisor));
            var writeCutoff = GetLonger(
                remaining - observationMargin,
                MinimumExecutionDuration);

            var proportionalReserve = TimeSpan.FromTicks(
                writeCutoff.Ticks / TerminalizationReserveDivisor);
            var terminalizationReserve = GetLonger(
                proportionalReserve,
                MinimumTerminalizationReserve);
            var maximumTerminalizationReserve =
                UnityMutationCancellationPolicy.QuiescenceGrace
                + MaximumCompletionPersistenceDuration
                + maximumResponseFrameWriteDuration;
            terminalizationReserve = GetShorter(
                terminalizationReserve,
                maximumTerminalizationReserve);
            terminalizationReserve = GetShorter(
                terminalizationReserve,
                writeCutoff - MinimumExecutionDuration);
            var executionCutoff = writeCutoff - terminalizationReserve;

            var terminalizationDuration = writeCutoff - executionCutoff;
            var responseWriteReserve = GetShorter(
                maximumResponseFrameWriteDuration,
                TimeSpan.FromTicks(terminalizationDuration.Ticks / 2));
            var persistenceCutoff = writeCutoff - responseWriteReserve;
            persistenceCutoff = GetShorter(
                persistenceCutoff,
                executionCutoff
                    + UnityMutationCancellationPolicy.QuiescenceGrace
                    + MaximumCompletionPersistenceDuration);
            persistenceCutoff = GetLonger(persistenceCutoff, executionCutoff);

            return new IpcRequestPhasePlan(
                executionCutoff,
                persistenceCutoff,
                writeCutoff);
        }

        private static void EnsureUtc (DateTimeOffset value, string parameterName)
        {
            if (value.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "IPC request phase timestamps must use the UTC offset.",
                    parameterName);
            }
        }

        private static TimeSpan GetShorter (TimeSpan first, TimeSpan second)
        {
            return first <= second ? first : second;
        }

        private static TimeSpan GetLonger (TimeSpan first, TimeSpan second)
        {
            return first >= second ? first : second;
        }
    }
}
