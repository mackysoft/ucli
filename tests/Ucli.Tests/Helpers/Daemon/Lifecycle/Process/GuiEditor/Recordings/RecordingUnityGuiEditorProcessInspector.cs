namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingUnityGuiEditorProcessInspector : IUnityGuiEditorProcessInspector
{
    private readonly List<int> inspectedProcessIds = [];

    public RecordingUnityGuiEditorProcessInspector (UnityGuiEditorProcessInspection result)
    {
        Result = result;
    }

    public UnityGuiEditorProcessInspection Result { get; set; }

    public IReadOnlyList<int> InspectedProcessIds => inspectedProcessIds;

    public UnityGuiEditorProcessInspection Inspect (int processId)
    {
        inspectedProcessIds.Add(processId);
        return Result;
    }
}
