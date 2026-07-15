using System.Reflection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcExplicitConstructorStateContractTests
{
    public static TheoryData<Type> ExplicitStateContractTypes => new()
    {
        typeof(IpcExecuteResponse),
        typeof(IpcPlayTransitionResult),
        typeof(IpcUnityBuildProfileInput),
        typeof(IpcIndexSceneTreeLiteReadRequest),
        typeof(IpcTestRunRequest),
        typeof(IpcUnityEditorObservation),
    };

    [Theory]
    [MemberData(nameof(ExplicitStateContractTypes))]
    [Trait("Size", "Small")]
    public void Constructor_ParametersHaveNoDefaultValues (Type contractType)
    {
        var constructor = Assert.Single(contractType.GetConstructors());

        Assert.All(constructor.GetParameters(), static parameter =>
        {
            Assert.False(parameter.IsOptional);
            Assert.False(parameter.HasDefaultValue);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildOutputLayoutResolver_AndroidAppBundleIntentIsRequiredBeforeOutput ()
    {
        var method = typeof(IpcBuildOutputLayoutResolver).GetMethod(
            nameof(IpcBuildOutputLayoutResolver.TryResolve),
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Equal(typeof(bool), parameters[2].ParameterType);
        Assert.Equal("androidAppBundle", parameters[2].Name);
        Assert.False(parameters[2].IsOptional);
        Assert.False(parameters[2].HasDefaultValue);
        Assert.True(parameters[3].IsOut);
    }
}
