using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class InitTemplateStoreAssert
{
    public static void DefaultConfigWritten (
        RecordingInitTemplateStore templateStore,
        bool expectedForce)
    {
        var invocation = Assert.Single(templateStore.Invocations);
        Assert.Equal(expectedForce, invocation.Force);
        AssertDefaultConfig(invocation.Config);
    }

    private static void AssertDefaultConfig (UcliConfig config)
    {
        Assert.Equal(1, config.SchemaVersion);
        Assert.Equal(OperationPolicy.Safe, config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Optional, config.PlanTokenMode);
        Assert.Equal(ReadIndexMode.RequireFresh, config.ReadIndexDefaultMode);
        Assert.Equal(["^ucli\\."], config.OperationAllowlist);
    }
}
