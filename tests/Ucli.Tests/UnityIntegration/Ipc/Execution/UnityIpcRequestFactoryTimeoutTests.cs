namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

public sealed class UnityIpcRequestFactoryDeadlineTests
{
    private static readonly DateTimeOffset RequestDeadlineUtc = new(
        2030,
        1,
        2,
        3,
        4,
        5,
        TimeSpan.Zero);

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(999)]
    public void UnityIpcRequestFactory_WhenMethodIsUndefined_ThrowsArgumentOutOfRangeException (int value)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcRequestFactory.Create(
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            (UnityIpcMethod)value,
            IpcPayloadCodec.SerializeToElement(new { }),
            Guid.NewGuid(),
            IpcResponseMode.Single,
            RequestDeadlineUtc,
            requestDeadlineRemainingMilliseconds: 1234));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcRequestFactory_WithRequestDeadline_PreservesDeadlineOnEnvelope ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcCompileRequest(RunIdTestValues.Compile));

        var request = UnityIpcRequestFactory.Create(
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1),
            UnityIpcMethod.Compile,
            payload,
            Guid.NewGuid(),
            IpcResponseMode.Single,
            RequestDeadlineUtc,
            requestDeadlineRemainingMilliseconds: 1234);

        Assert.Equal(RequestDeadlineUtc, request.RequestDeadlineUtc);
        Assert.Equal(1234, request.RequestDeadlineRemainingMilliseconds);
        Assert.Equal(payload.GetRawText(), request.Payload.GetRawText());
        Assert.False(request.Payload.TryGetProperty("timeoutMilliseconds", out _));
    }
}
