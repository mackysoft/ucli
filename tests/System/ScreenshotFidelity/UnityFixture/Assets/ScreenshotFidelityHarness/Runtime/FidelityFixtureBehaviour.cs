using UnityEngine;
using UnityEngine.UI;

namespace MackySoft.Ucli.ScreenshotFidelity
{
    /// <summary> Keeps fixture geometry and resolution markers synchronized with the presented GameView surface. </summary>
    [ExecuteAlways]
    public sealed class FidelityFixtureBehaviour : MonoBehaviour
    {
        private const int EncodedBitCount = 12;

        private Camera fixtureCamera;

        private Transform patternTransform;

        private Image[] resolutionBits;

        /// <summary> Connects the request-independent fixture objects created by the Editor harness. </summary>
        public void Configure (
            Camera camera,
            Transform pattern,
            Image[] bits)
        {
            fixtureCamera = camera;
            patternTransform = pattern;
            resolutionBits = bits;
            RefreshPresentationMarkers();
        }

        /// <summary> Refreshes presentation markers before a fixture repaint. </summary>
        public void RefreshPresentationMarkers ()
        {
            if (fixtureCamera == null || patternTransform == null)
            {
                return;
            }

            var height = fixtureCamera.orthographicSize * 2f;
            var width = height * Mathf.Max(fixtureCamera.aspect, 0.01f);
            patternTransform.localScale = new Vector3(width, height, 1f);

            if (resolutionBits == null || resolutionBits.Length != EncodedBitCount * 2)
            {
                return;
            }

            var maximumEncodedValue = (1 << EncodedBitCount) - 1;
            var pixelWidth = Mathf.Clamp(fixtureCamera.pixelWidth, 0, maximumEncodedValue);
            var pixelHeight = Mathf.Clamp(fixtureCamera.pixelHeight, 0, maximumEncodedValue);
            for (var index = 0; index < EncodedBitCount; index++)
            {
                var shift = EncodedBitCount - index - 1;
                resolutionBits[index].color = EncodeBit((pixelWidth & 1 << shift) != 0);
                resolutionBits[index + EncodedBitCount].color = EncodeBit((pixelHeight & 1 << shift) != 0);
            }
        }

        private static Color EncodeBit (bool value)
        {
            return value
                ? new Color(0f, 1f, 1f, 1f)
                : new Color(1f, 0f, 1f, 1f);
        }

        private void OnGUI ()
        {
            var previousColor = GUI.color;
            try
            {
                // This normalized asymmetric signature lets the oracle prove that runtime IMGUI reached the GameView.
                var width = Mathf.Max(Screen.width, 1);
                var height = Mathf.Max(Screen.height, 1);
                GUI.color = Color.white;
                GUI.DrawTexture(
                    new Rect(width * 0.020f, height * 0.070f, width * 0.035f, height * 0.015f),
                    Texture2D.whiteTexture);
                GUI.color = Color.black;
                GUI.DrawTexture(
                    new Rect(width * 0.020f, height * 0.085f, width * 0.020f, height * 0.015f),
                    Texture2D.whiteTexture);
                GUI.color = Color.cyan;
                GUI.DrawTexture(
                    new Rect(width * 0.040f, height * 0.085f, width * 0.015f, height * 0.015f),
                    Texture2D.whiteTexture);
            }
            finally
            {
                GUI.color = previousColor;
            }
        }
    }
}
