using UnityEngine;

public class SampleInputSystem
{
    public void UpdateInput(GameObject entity)
    {
        // Example: move player with input
        var input = entity.GetComponent<InputComponent>();
        var transform = entity.GetComponent<TransformComponent>();
        if (input != null && transform != null)
        {
            if (Input.GetKey(KeyCode.W))
                transform.position.y += 1;
            if (Input.GetKey(KeyCode.S))
                transform.position.y -= 1;
        }
    }
}
