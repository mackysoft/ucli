using System;
using System.IO;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using UnityEditor.TestTools.TestRunner.Api;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Writes Unity Test Framework results XML artifacts to filesystem paths. </summary>
    internal sealed class UnityTestResultsXmlWriter : IUnityTestResultsXmlWriter
    {
        /// <summary> Writes one Unity test result adaptor to a results XML file path. </summary>
        /// <param name="testResult"> The Unity test result adaptor. </param>
        /// <param name="resultsXmlPath"> The output XML path. </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="testResult" /> or <paramref name="resultsXmlPath" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="InvalidOperationException"> Thrown when output directory path cannot be resolved. </exception>
        public void Write (
            ITestResultAdaptor testResult,
            AbsolutePath resultsXmlPath)
        {
            if (testResult == null)
            {
                throw new ArgumentNullException(nameof(testResult));
            }

            if (resultsXmlPath == null)
            {
                throw new ArgumentNullException(nameof(resultsXmlPath));
            }

            if (!resultsXmlPath.TryGetParent(out var resultsDirectoryPath))
            {
                throw new InvalidOperationException($"resultsXmlPath directory could not be resolved: {resultsXmlPath.Value}");
            }

            UcliLocalStorageBootstrapper.EnsureInitialized(resultsDirectoryPath);
            Directory.CreateDirectory(resultsDirectoryPath.Value);
            TestRunnerApi.SaveResultToFile(testResult, resultsXmlPath.Value);
            if (!File.Exists(resultsXmlPath.Value))
            {
                throw new IOException($"Failed to write test results xml: {resultsXmlPath.Value}");
            }
        }
    }
}
