using UnityEngine;

public class SampleHealthSystem
{
    public void UpdateHealth(GameObject entity)
    {
        // Example: reduce health if entity is an enemy
        var health = entity.GetComponent<HealthComponent>();
        if (health != null && entity.CompareTag("Enemy"))
        {
            health.current -= 1;
        }
    }
}
