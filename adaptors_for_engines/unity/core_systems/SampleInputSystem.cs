using Mycology_ECS.adaptors_for_engines.unity.core_systems;
using Rewired;
using UnityEngine;

public class SampleInputSystem
{
    private const int DefaultPlayerId = 0;

    // Configure these action names in the Rewired Input Manager.
    // Recommended: map WASD (or stick) to MoveHorizontal/MoveVertical axes.
    // Fallbacks: MoveUp/MoveDown and MoveLeft/MoveRight buttons.
    // Movement is applied in XZ (X = left/right, Z = up/down).
    private const string MoveHorizontalAction = "MoveHorizontal";
    private const string MoveVerticalAction = "MoveVertical";
    private const string MoveUpAction = "MoveUp";
    private const string MoveDownAction = "MoveDown";
    private const string MoveLeftAction = "MoveLeft";
    private const string MoveRightAction = "MoveRight";

    public void UpdateInput(GameObject entity)
    {
        var input = entity.GetComponent<InputComponent>();
        var transformComponent = entity.GetComponent<TransformComponent>();
        if (input != null)
        {
            var delta = 400f * Time.deltaTime;

            var playerId = ResolvePlayerId(input.scheme);
            if (TryGetPlayer(playerId, out var player))
            {
                var horizontal = ReadHorizontal(player);
                var vertical = ReadVertical(player);
                if (!Mathf.Approximately(horizontal, 0f) || !Mathf.Approximately(vertical, 0f))
                {
                    // X = right/left, Z = up/down
                    entity.transform.localPosition += new Vector3(horizontal * delta, 0f, vertical * delta);
                }
            }

            if (transformComponent != null)
            {
                transformComponent.position = entity.transform.localPosition;
            }
        }
    }

    private static float ReadHorizontal(Player player)
    {
        // Use an axis if present, otherwise fall back to left/right buttons.
        var horizontalActionId = ReInput.mapping.GetActionId(MoveHorizontalAction);
        var horizontal = GetAxis(player, horizontalActionId);
        if (!Mathf.Approximately(horizontal, 0f)) return horizontal;

        var leftActionId = ReInput.mapping.GetActionId(MoveLeftAction);
        var rightActionId = ReInput.mapping.GetActionId(MoveRightAction);
        if (GetButton(player, rightActionId)) horizontal += 1f;
        if (GetButton(player, leftActionId)) horizontal -= 1f;
        return Mathf.Clamp(horizontal, -1f, 1f);
    }

    private static int ResolvePlayerId(string scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme)) return DefaultPlayerId;

        var s = scheme.Trim();
        if (string.Equals(s, "keyboard", System.StringComparison.OrdinalIgnoreCase)) return DefaultPlayerId;

        // Common variants: "p0", "p1", "player0", "player1", "0", "1".
        if (s.Length >= 2 && (s[0] == 'p' || s[0] == 'P'))
        {
            if (int.TryParse(s.Substring(1), out var pidFromP)) return pidFromP;
        }

        if (s.StartsWith("player", System.StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(s.Substring("player".Length), out var pidFromPlayer)) return pidFromPlayer;
        }

        return int.TryParse(s, out var pid) ? pid : DefaultPlayerId;
    }

    private static bool TryGetPlayer(int playerId, out Player player)
    {
        player = null;
        if (!ReInput.isReady) return false;
        if (playerId < 0 || playerId >= ReInput.players.playerCount) return false;

        player = ReInput.players.GetPlayer(playerId);
        return player != null;
    }

    private static float ReadVertical(Player player)
    {
        // Use an axis if present, otherwise fall back to up/down buttons.
        var verticalActionId = ReInput.mapping.GetActionId(MoveVerticalAction);
        var vertical = GetAxis(player, verticalActionId);
        if (!Mathf.Approximately(vertical, 0f)) return vertical;

        var upActionId = ReInput.mapping.GetActionId(MoveUpAction);
        var downActionId = ReInput.mapping.GetActionId(MoveDownAction);
        if (GetButton(player, upActionId)) vertical += 1f;
        if (GetButton(player, downActionId)) vertical -= 1f;
        return Mathf.Clamp(vertical, -1f, 1f);
    }

    private static bool GetButton(Player player, int actionId)
    {
        if (actionId < 0) return false; // silence Rewired warnings for missing actions
        return player.GetButton(actionId);
    }

    private static float GetAxis(Player player, int actionId)
    {
        if (actionId < 0) return 0f; // silence Rewired warnings for missing actions
        return player.GetAxis(actionId);
    }
}
