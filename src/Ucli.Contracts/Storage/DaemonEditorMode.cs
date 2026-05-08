namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines supported daemon Editor modes. </summary>
public enum DaemonEditorMode
{
    /// <summary> Unity Editor running in batchmode. </summary>
    Batchmode = 0,

    /// <summary> Unity Editor running with the graphical user interface. </summary>
    Gui = 1,
}
