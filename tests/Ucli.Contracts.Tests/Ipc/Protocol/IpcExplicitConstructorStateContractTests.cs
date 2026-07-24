using System.Reflection;
using MackySoft.Ucli.Contracts.Assurance.Build;
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
    public void BuildOutputLayoutPolicy_UsesTypedInputsAndRequiresAndroidAppBundleIntent ()
    {
        var method = Assert.Single(typeof(BuildPipelineOutputLayoutPolicy).GetMethods(
            BindingFlags.Public | BindingFlags.Static));
        var parameters = method.GetParameters();

        Assert.Equal(nameof(BuildPipelineOutputLayoutPolicy.TryResolve), method.Name);
        Assert.DoesNotContain(parameters, static parameter => parameter.ParameterType == typeof(string));
        Assert.Equal(typeof(BuildTargetStableName), parameters[0].ParameterType);
        Assert.Equal(typeof(bool), parameters[1].ParameterType);
        Assert.Equal("androidAppBundle", parameters[1].Name);
        Assert.False(parameters[1].IsOptional);
        Assert.False(parameters[1].HasDefaultValue);
        Assert.True(parameters[2].IsOut);
        Assert.Equal(
            typeof(BuildPipelineOutputLayoutDefinition).MakeByRefType(),
            parameters[2].ParameterType);
    }
}
