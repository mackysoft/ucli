using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class IpcGameViewRecordingRequestContractGuard
{
    public static IpcGameViewRecordingSnapshot? RequireKnownRecording (
        IpcGameViewRecordingSnapshot? knownRecording,
        Guid recordingId,
        Sha256Digest requestDigest,
        int effectiveMaxDurationSeconds,
        IpcGameViewRecordingStartBinding startBinding,
        string parameterName)
    {
        if (knownRecording is not null
            && (recordingId != knownRecording.RecordingId
                || requestDigest != knownRecording.RequestDigest
                || effectiveMaxDurationSeconds != knownRecording.EffectiveMaxDurationSeconds
                || startBinding.Runtime != knownRecording.Runtime
                || startBinding.Generation != knownRecording.StartGeneration
                || knownRecording.IsTerminal))
        {
            throw new ArgumentException(
                "A known recording must match the fixed non-terminal recording start facts.",
                parameterName);
        }

        return knownRecording;
    }
}
