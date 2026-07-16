namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Receives Unity Test Framework progress events from synchronous callbacks. </summary>
    internal interface IUnityTestRunProgressSink
    {
        /// <summary> Queues one progress event for delivery. </summary>
        /// <param name="eventName"> The stream event name. </param>
        /// <param name="payload"> The stream event payload. </param>
        void Publish (
            string eventName,
            object payload);
    }
}
