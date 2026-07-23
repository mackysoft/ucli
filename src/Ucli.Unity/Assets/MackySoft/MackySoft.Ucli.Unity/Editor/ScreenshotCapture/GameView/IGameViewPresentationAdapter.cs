using UnityEditor;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView
{
    /// <summary> Provides the GameView presentation operations required by capture and deferred restoration. </summary>
    internal interface IGameViewPresentationAdapter
    {
        bool TryGetSource (out GameViewPresentationSource source, out string errorMessage);

        bool TryValidateSource (GameViewPresentationSource source, out string errorMessage);

        bool IsCurrentTarget (EditorWindow gameView);

        bool TryRepaintImmediately (EditorWindow gameView, out string errorMessage);
    }
}
