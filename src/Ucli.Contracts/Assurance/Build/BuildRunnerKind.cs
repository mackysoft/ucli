using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines the supported build runner kinds. </summary>
public enum BuildRunnerKind
{
    /// <summary> Invokes Unity <c>BuildPipeline</c> through the uCLI Unity runtime. </summary>
    [UcliContractLiteral("buildPipeline")]
    BuildPipeline = 1,

    /// <summary> Invokes a Unity editor-side static method through the uCLI build runner bridge. </summary>
    [UcliContractLiteral("executeMethod")]
    ExecuteMethod = 2,
}
