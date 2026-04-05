using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks persistence resources that were changed by the current request. </summary>
    internal sealed class RequestAttributedChangeRegistry
    {
        private readonly HashSet<OperationResource> resources = new HashSet<OperationResource>();

        /// <summary> Marks one persistence resource as changed by the current request. </summary>
        /// <param name="resource"> The persistence resource to track. </param>
        public void MarkChanged (OperationResource resource)
        {
            resources.Add(resource);
        }

        /// <summary> Determines whether one persistence resource is currently tracked as changed by the request. </summary>
        /// <param name="resource"> The persistence resource to test. </param>
        /// <returns> <see langword="true" /> when <paramref name="resource" /> is tracked as changed; otherwise <see langword="false" />. </returns>
        public bool Contains (OperationResource resource)
        {
            return resources.Contains(resource);
        }

        /// <summary> Removes one persistence resource from the request-attributed change set. </summary>
        /// <param name="resource"> The persistence resource to unmark. </param>
        public void UnmarkChanged (OperationResource resource)
        {
            resources.Remove(resource);
        }

        /// <summary> Copies the tracked request-attributed resources into the destination collection. </summary>
        /// <param name="destination"> The destination collection that receives every tracked resource. Must not be <see langword="null" />. </param>
        /// <exception cref="System.ArgumentNullException"> Thrown when <paramref name="destination" /> is <see langword="null" />. </exception>
        public void CopyTo (ICollection<OperationResource> destination)
        {
            if (destination == null)
            {
                throw new System.ArgumentNullException(nameof(destination));
            }

            foreach (var resource in resources)
            {
                destination.Add(resource);
            }
        }

        /// <summary> Clears all request-attributed change markers. </summary>
        public void ClearAll ()
        {
            resources.Clear();
        }
    }
}
