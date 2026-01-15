using Mycology_ECS.adaptors_for_engines.unity.core_systems;
using UnityEngine;

public class SampleAISystem
{
    public void UpdateAI(GameObject entity)
    {
        var ai = entity.GetComponent<AIComponent>();
        var transformComponent = entity.GetComponent<TransformComponent>();
        if (ai != null)
        {
            var pos = entity.transform.localPosition;
            pos.x += Mathf.Sin(Time.time) * Time.deltaTime * 2f;
            entity.transform.localPosition = pos;

            if (transformComponent != null)
            {
                transformComponent.position = pos;
            }
        }
    }
}
