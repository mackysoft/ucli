using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Tests.Helpers.Configuration;

internal static class UcliConfigStoreAssert
{
    public static RecordingUcliConfigStore.SaveInvocation ConfigSavedFor (
        RecordingUcliConfigStore configStore,
        AbsolutePath expectedStorageRoot,
        UcliConfig? expectedConfig = null)
    {
        var invocation = Assert.Single(configStore.SaveInvocations);
        Assert.Equal(expectedStorageRoot, invocation.StorageRoot);
        if (expectedConfig is not null)
        {
            Assert.Equal(expectedConfig, invocation.Config);
        }

        return invocation;
    }
}
