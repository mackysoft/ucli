using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class IndexCatalogTestAsset : ScriptableObject
    {
#pragma warning disable CS0414 // Unity reads this serialized test field through reflection.
        [SerializeField]
        private float speed = 3.5f;
#pragma warning restore CS0414
    }
}
