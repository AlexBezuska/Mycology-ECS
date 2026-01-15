using UnityEngine;

namespace Mycology_ECS.adaptors_for_engines.unity.core_systems
{
    public sealed class HealthComponent : MonoBehaviour
    {
        public float max;
        public float current;
    }

    public sealed class AIComponent : MonoBehaviour
    {
        public string behavior;
    }

    public sealed class InputComponent : MonoBehaviour
    {
        public string scheme;
    }

    public sealed class TransformComponent : MonoBehaviour
    {
        public Vector3 position;
        public float rotation;

        private bool _applied;

        private void OnEnable()
        {
            ApplyOnce();
        }

        public void ApplyOnce()
        {
            if (_applied) return;
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            _applied = true;
            enabled = false;
        }
    }
}
