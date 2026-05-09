namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.GuiEditor;

/// <summary> Provides OS process metadata for Unity GUI Editor candidate verification. </summary>
internal interface IUnityGuiEditorProcessInspector
{
    /// <summary> Inspects one process identifier without deciding whether it is an accepted GUI Editor candidate. </summary>
    UnityGuiEditorProcessInspection Inspect (int processId);
}
