using Mycology_ECS.adaptors_for_engines.unity.core_systems;
using UnityEngine;

public class SampleHealthSystem
{
    public void UpdateHealth(GameObject entity)
    {
        // Example: reduce health if entity is an enemy
        var health = entity.GetComponent<HealthComponent>();
        if (health != null && CompareTagSafe(entity, "Enemy"))
        {
            health.current -= 1;
        }
    }

    private static bool CompareTagSafe(GameObject entity, string tag)
    {
        if (entity == null || string.IsNullOrWhiteSpace(tag)) return false;
        try
        {
            return entity.CompareTag(tag);
        }
        catch
        {
            return false;
        }
    }
}
