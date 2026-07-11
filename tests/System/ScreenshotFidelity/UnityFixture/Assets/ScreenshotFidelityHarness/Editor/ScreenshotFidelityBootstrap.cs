using System;
using UnityEditor;

namespace MackySoft.Ucli.ScreenshotFidelity
{
    /// <summary> Starts the GUI fidelity fixture controller in an explicitly marked Unity process. </summary>
    [InitializeOnLoad]
    public static class ScreenshotFidelityBootstrap
    {
        private const string RunDirectoryArgument = "-ucliScreenshotFidelityRunDirectory";

        static ScreenshotFidelityBootstrap ()
        {
            TryStartFromCommandLine();
        }

        private static void TryStartFromCommandLine ()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], RunDirectoryArgument, StringComparison.Ordinal))
                {
                    ScreenshotFidelityController.Start(arguments[index + 1]);
                    return;
                }
            }
        }
    }
}
