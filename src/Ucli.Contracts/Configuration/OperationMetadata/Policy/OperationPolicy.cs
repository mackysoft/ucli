using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines allowed operation safety levels configured in <c>.ucli/config.json</c>. </summary>
public enum OperationPolicy
{
    /// <summary> Allows only safe operations. </summary>
    [UcliContractLiteral("safe")]
    Safe = 0,

    /// <summary> Allows safe and advanced operations. </summary>
    [UcliContractLiteral("advanced")]
    Advanced = 1,

    /// <summary> Allows safe, advanced, and dangerous operations. </summary>
    [UcliContractLiteral("dangerous")]
    Dangerous = 2,
}
