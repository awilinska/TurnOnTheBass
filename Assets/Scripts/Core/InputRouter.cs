using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TurnOnTheBass
{
    public static class InputRouter
    {
        public static Vector2 GetMovement()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                Vector2 input = new Vector2(x, y);
                return input.sqrMagnitude > 1f ? input.normalized : input;
            }
#endif
            return Vector2.zero;
        }

        public static bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
            return false;
        }

        public static bool WasConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
            return false;
        }

        public static bool WasLanePressed(int lane)
        {
            if (lane < 0 || lane >= RhythmMinigame.LaneCount)
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                switch (lane)
                {
                    case 0:
                        return keyboard.aKey.wasPressedThisFrame;
                    case 1:
                        return keyboard.sKey.wasPressedThisFrame;
                    case 2:
                        return keyboard.dKey.wasPressedThisFrame;
                    case 3:
                        return keyboard.fKey.wasPressedThisFrame;
                }
            }
#endif
            return false;
        }
    }
}
