using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines supported daemon Editor modes. </summary>
public enum DaemonEditorMode
{
    /// <summary> Unity Editor running in batchmode. </summary>
    [UcliContractLiteral("batchmode")]
    Batchmode = 1,

    /// <summary> Unity Editor running with the graphical user interface. </summary>
    [UcliContractLiteral("gui")]
    Gui = 2,
}
