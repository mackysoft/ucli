using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class DaemonDiagnosisStartupPhaseContractLiteralTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonDiagnosisStartupPhase.ScriptCompilation, "scriptCompilation")]
    [InlineData(DaemonDiagnosisStartupPhase.PackageResolution, "packageResolution")]
    [InlineData(DaemonDiagnosisStartupPhase.UserAction, "userAction")]
    [InlineData(DaemonDiagnosisStartupPhase.ProcessExit, "processExit")]
    [InlineData(DaemonDiagnosisStartupPhase.EndpointRegistration, "endpointRegistration")]
    public void DaemonDiagnosisStartupPhaseContractLiteral_ToValue_ReturnsCanonicalLiteral (
        DaemonDiagnosisStartupPhase phase,
        string expectedValue)
    {
        Assert.Equal(expectedValue, ContractLiteralCodec.ToValue(phase));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("scriptCompilation", true)]
    [InlineData(" endpointRegistration ", false)]
    [InlineData("SCRIPT_COMPILATION", false)]
    [InlineData("unsupported", false)]
    public void IsSupported_UsesCanonicalContractLiteral (
        string value,
        bool expectedResult)
    {
        Assert.Equal(expectedResult, DaemonDiagnosisStartupPhaseValues.IsSupported(value));
    }
}
