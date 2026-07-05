using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Tests.Helpers.Configuration;

internal static class UcliConfigStoreAssert
{
    public static RecordingUcliConfigStore.SaveInvocation ConfigSavedFor (
        RecordingUcliConfigStore configStore,
        string expectedStorageRoot,
        UcliConfig? expectedConfig = null)
    {
        var invocation = Assert.Single(configStore.SaveInvocations);
        FileSystemAssert.ForPath(invocation.StorageRoot).EqualsNormalized(expectedStorageRoot);
        if (expectedConfig is not null)
        {
            Assert.Equal(expectedConfig, invocation.Config);
        }

        return invocation;
    }
}
