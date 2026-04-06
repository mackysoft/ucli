using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CompOperationTestComponent : MonoBehaviour
    {
        [SerializeField]
        private int integerValue = 1;

        [SerializeField]
        private float floatValue = 1.5f;

        [SerializeField]
        private string text = "before";

        [SerializeField]
        private SampleMode enumValue = SampleMode.First;

        [SerializeField]
        private GameObject? objectReferenceValue;

        [SerializeField]
        private Component? componentReferenceValue;

        [SerializeField]
        private ExposedReference<GameObject> exposedObjectReferenceValue;

        [SerializeField]
        private NestedValue nestedValue = new NestedValue();

        [SerializeField]
        private List<NestedValue> nestedList = new List<NestedValue>
        {
            new NestedValue
            {
                Number = 1,
                Label = "initial",
            },
        };

        [SerializeReference]
        private ManagedBase? managedReferenceValue;

        [SerializeField]
        private AnimationCurve curveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField]
        private Gradient gradientValue = CreateDefaultGradient();

        [SerializeField]
        private Bounds boundsValue = new Bounds(Vector3.zero, Vector3.one);

        [SerializeField]
        private Hash128 hashValue = default;

        public int IntegerValue => integerValue;

        public float FloatValue => floatValue;

        public string Text => text;

        public SampleMode EnumValue => enumValue;

        public GameObject? ObjectReferenceValue => objectReferenceValue;

        public Component? ComponentReferenceValue => componentReferenceValue;

        public GameObject? ExposedObjectReferenceValue => exposedObjectReferenceValue.defaultValue as GameObject;

        public NestedValue NestedValueValue => nestedValue;

        public IReadOnlyList<NestedValue> NestedList => nestedList;

        public ManagedBase? ManagedReferenceValue => managedReferenceValue;

        public AnimationCurve CurveValue => curveValue;

        public Gradient GradientValue => gradientValue;

        public Bounds BoundsValue => boundsValue;

        public Hash128 HashValue => hashValue;

        public enum SampleMode
        {
            First = 0,
            Second = 1,
            Third = 2,
        }

        [Serializable]
        public sealed class NestedValue
        {
            [SerializeField]
            private int number;

            [SerializeField]
            private string label = string.Empty;

            public int Number
            {
                get => number;
                set => number = value;
            }

            public string Label
            {
                get => label;
                set => label = value;
            }
        }

        [Serializable]
        public abstract class ManagedBase
        {
        }

        [Serializable]
        public sealed class ManagedValue : ManagedBase
        {
            [SerializeField]
            private int amount;

            [SerializeField]
            private string note = string.Empty;

            public int Amount => amount;

            public string Note => note;
        }

        private static Gradient CreateDefaultGradient ()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.black, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            gradient.mode = GradientMode.Blend;
            return gradient;
        }
    }
}
