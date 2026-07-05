using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class IpcStreamFrameAssert
{
    public static IpcStreamFrame SingleEvent (
        IReadOnlyList<IpcStreamFrame> frames,
        string expectedEvent)
    {
        var frame = Assert.Single(frames);
        Assert.Equal(expectedEvent, frame.Event);
        return frame;
    }
}
