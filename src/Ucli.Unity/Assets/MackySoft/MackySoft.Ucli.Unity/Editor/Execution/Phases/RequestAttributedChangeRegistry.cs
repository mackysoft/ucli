using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks persistence resources that were changed by the current request. </summary>
    internal sealed class RequestAttributedChangeRegistry
    {
        private readonly HashSet<OperationResource> resources = new HashSet<OperationResource>();

        public void MarkChanged (OperationResource resource)
        {
            resources.Add(resource);
        }

        public bool Contains (OperationResource resource)
        {
            return resources.Contains(resource);
        }

        public void UnmarkChanged (OperationResource resource)
        {
            resources.Remove(resource);
        }

        public void ClearAll ()
        {
            resources.Clear();
        }
    }
}
