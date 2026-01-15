using Mycology_ECS.adaptors_for_engines.unity.core_systems;
using UnityEngine;

namespace Mycology_ECS.translation_layers.unity
{
    public sealed class MycoSystemsRunner : MonoBehaviour
    {
        private readonly SampleInputSystem _input = new();
        private readonly SampleAISystem _ai = new();
        private readonly SampleHealthSystem _health = new();

        private void Update()
        {
            var manager = Myco_Unity_Entities.Instance;
            if (manager == null) return;

            manager.ForEachSpawnedInstance((_, go) =>
            {
                if (go == null) return;

                if (go.GetComponent<InputComponent>() != null)
                {
                    _input.UpdateInput(go);
                }

                if (go.GetComponent<AIComponent>() != null)
                {
                    _ai.UpdateAI(go);
                }

                if (go.GetComponent<HealthComponent>() != null)
                {
                    _health.UpdateHealth(go);
                }
            });
        }
    }
}
