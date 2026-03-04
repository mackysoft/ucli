using UnityEditor.TestTools.TestRunner.Api;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Writes Unity Test Framework results XML artifacts. </summary>
    internal interface IUnityTestResultsXmlWriter
    {
        /// <summary> Writes one Unity test result adaptor to a results XML file path. </summary>
        /// <param name="testResult"> The Unity test result adaptor. </param>
        /// <param name="resultsXmlPath"> The output XML path. </param>
        void Write (
            ITestResultAdaptor testResult,
            string resultsXmlPath);
    }
}
