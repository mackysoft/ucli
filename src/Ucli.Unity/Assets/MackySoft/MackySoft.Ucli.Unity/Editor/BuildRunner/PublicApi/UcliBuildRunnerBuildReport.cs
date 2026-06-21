using System;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents optional BuildReport evidence returned by a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerBuildReport
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerBuildReport" /> class. </summary>
        /// <param name="path"> The BuildReport JSON source path relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </param>
        public UcliBuildRunnerBuildReport (string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path must not be empty.", nameof(path));
            }

            Path = path;
        }

        /// <summary> Gets the BuildReport JSON source path relative to <see cref="UcliBuildRunnerContext.OutputDir" />. </summary>
        public string Path { get; }
    }
}
