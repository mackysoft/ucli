using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class AssetOperationTestAsset : ScriptableObject
    {
        [SerializeField]
        private int integerValue = 1;

        [SerializeField]
        private string text = "before";

        public int IntegerValue => integerValue;

        public string Text => text;
    }
}