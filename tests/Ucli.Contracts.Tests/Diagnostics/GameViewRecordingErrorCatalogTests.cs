namespace MackySoft.Ucli.Contracts.Tests.Diagnostics;

public sealed class GameViewRecordingErrorCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void RecordingCodes_AreRegisteredWithOwningCommands ()
    {
        var descriptors = UcliKnownErrorDescriptors.All
            .Where(static descriptor => GameViewRecordingErrorCodes.All.Contains(descriptor.Code))
            .ToArray();

        Assert.Equal(GameViewRecordingErrorCodes.All.Count, descriptors.Length);
        Assert.All(descriptors, static descriptor => Assert.Equal("recording", descriptor.Category));
        Assert.All(descriptors, static descriptor =>
            Assert.All(descriptor.AppliesTo, static command =>
                Assert.Contains(command, new[]
                {
                    UcliCommandIds.RecordingStart,
                    UcliCommandIds.RecordingStatus,
                    UcliCommandIds.RecordingStop,
                })));
        Assert.Contains(
            descriptors,
            static descriptor => descriptor.Code == GameViewRecordingErrorCodes.MonitoringTimeout
                && descriptor.AppliesTo.SequenceEqual(new[] { UcliCommandIds.RecordingStart }));
        var dispatchDescriptors = descriptors
            .Where(static descriptor => descriptor.Code == GameViewRecordingErrorCodes.BindingMismatch
                || descriptor.Code == GameViewRecordingErrorCodes.DispatchDeadlineExceeded)
            .ToArray();
        Assert.Equal(2, dispatchDescriptors.Length);
        Assert.All(
            dispatchDescriptors,
            static descriptor =>
            {
                Assert.Equal(
                    new[]
                    {
                        UcliCommandIds.RecordingStart,
                        UcliCommandIds.RecordingStatus,
                        UcliCommandIds.RecordingStop,
                    },
                    descriptor.AppliesTo);
                Assert.True(descriptor.ExecutionSemantics.ImpliesNotApplied);
                Assert.True(descriptor.ExecutionSemantics.MayBeIndeterminate);
            });
    }
}
