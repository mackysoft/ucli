namespace MackySoft.Ucli.Tests.Helpers.Indexing;

internal static class ReadIndexInputFingerprintAssert
{
    public static void CoreFingerprintRequested (RecordingReadIndexInputFingerprintProvider fingerprintProvider)
    {
        Assert.NotEmpty(fingerprintProvider.CoreInvocations);
    }
}
