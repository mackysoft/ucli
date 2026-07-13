namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Provides numeric validation shared by screenshot presentation probes and mappings. </summary>
    internal static class UnityScreenshotMath
    {
        /// <summary> Returns whether the value is neither NaN nor infinite. </summary>
        public static bool IsFinite (float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary> Returns whether the value is finite and greater than zero. </summary>
        public static bool IsFinitePositive (float value)
        {
            return value > 0f && IsFinite(value);
        }
    }
}
