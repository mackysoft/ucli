using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc.Protocol;

public sealed class IpcFrameReadResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Success_WhenValueIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(
            static () => IpcFrameReadResult<string>.Success(null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorKindIsNone_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => IpcFrameReadResult<string>.Failure(IpcFrameReadErrorKind.None, "failure"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenErrorMessageIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(
            static () => IpcFrameReadResult<string>.Failure(IpcFrameReadErrorKind.PayloadJsonInvalid, null!));
    }
}
