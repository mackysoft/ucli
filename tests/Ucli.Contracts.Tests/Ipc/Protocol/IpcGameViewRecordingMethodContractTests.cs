using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class IpcGameViewRecordingMethodContractTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.RecordingCapability, "recording.capability", true)]
    [InlineData(UnityIpcMethod.RecordingStart, "recording.start", false)]
    [InlineData(UnityIpcMethod.RecordingStatus, "recording.status", true)]
    [InlineData(UnityIpcMethod.RecordingStop, "recording.stop", false)]
    public void RecordingMethods_ExposeStableNamesAndReplayClassification (
        UnityIpcMethod method,
        string expectedText,
        bool supportsStatelessReadReplay)
    {
        Assert.Equal(expectedText, TextVocabulary.GetText(method));
        Assert.Equal(
            supportsStatelessReadReplay,
            UnityIpcMethodCapabilities.SupportsStatelessReadReplay(method));
        Assert.False(UnityIpcMethodCapabilities.SupportsStreaming(method));
        Assert.False(UnityIpcMethodCapabilities.SupportsLifecycleExecution(method));
    }
}
