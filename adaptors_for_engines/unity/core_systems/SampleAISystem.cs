using UnityEngine;

public class SampleAISystem
{
    public void UpdateAI(GameObject entity)
    {
        // Example: simple patrol behavior
        var ai = entity.GetComponent<AIComponent>();
        var transform = entity.GetComponent<TransformComponent>();
        if (ai != null && transform != null)
        {
            transform.position.x += Mathf.Sin(Time.time);
        }
    }
}
