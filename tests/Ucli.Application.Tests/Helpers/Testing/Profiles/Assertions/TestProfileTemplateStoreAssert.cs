using MackySoft.Ucli.Application.Features.Testing.Profiles;

namespace MackySoft.Ucli.Application.Tests;

internal static class TestProfileTemplateStoreAssert
{
    public static void DefaultProfileWritten (
        RecordingTestProfileTemplateStore templateStore,
        string? expectedOutputPath,
        bool expectedForce)
    {
        var invocation = Assert.Single(templateStore.Invocations);
        Assert.Equal(expectedOutputPath, invocation.OutputPath);
        Assert.Equal(expectedForce, invocation.Force);
        AssertDefaultProfile(invocation.Profile);
    }

    private static void AssertDefaultProfile (TestProfile profile)
    {
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal(".", profile.ProjectPath);
        Assert.Null(profile.UnityVersion);
        Assert.Null(profile.UnityEditorPath);
        Assert.Equal("editmode", profile.TestPlatform);
        Assert.Null(profile.TestFilter);
        Assert.Empty(profile.TestCategories);
        Assert.Empty(profile.AssemblyNames);
        Assert.Null(profile.TestSettingsPath);
        Assert.Equal(1800000, profile.Timeout);
    }
}
