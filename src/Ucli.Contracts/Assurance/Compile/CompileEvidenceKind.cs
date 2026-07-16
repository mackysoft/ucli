using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies the evidence category attached to a compile assurance claim. </summary>
public enum CompileEvidenceKind
{
    /// <summary> Script-compilation evidence. </summary>
    [UcliContractLiteral("scriptCompilation")]
    ScriptCompilation = 1,

    /// <summary> Domain-reload evidence. </summary>
    [UcliContractLiteral("domainReload")]
    DomainReload = 2,

    /// <summary> Unity lifecycle snapshot evidence. </summary>
    [UcliContractLiteral("lifecycleSnapshot")]
    LifecycleSnapshot = 3,
}
