using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TurnOnTheBass
{
    public static class InputRouter
    {
        private const float ExternalPressMaxAge = 0.18f;
        private static readonly float[] ExternalLanePressedAt =
        {
            -1000f,
            -1000f,
            -1000f,
            -1000f
        };

        public static void QueueExternalLanePress(int lane)
        {
            if (lane < 0 || lane >= RhythmMinigame.LaneCount)
            {
                return;
            }

            ExternalLanePressedAt[lane] = Time.unscaledTime;
        }

        public static void ClearExternalLanePresses()
        {
            for (int lane = 0; lane < ExternalLanePressedAt.Length; lane++)
            {
                ExternalLanePressedAt[lane] = -1000f;
            }
        }

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

            bool externalPressed = false;
            if ((Time.unscaledTime - ExternalLanePressedAt[lane]) <= ExternalPressMaxAge)
            {
                externalPressed = true;
                ExternalLanePressedAt[lane] = -1000f;
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                switch (lane)
                {
                    case 0:
                        return keyboard.aKey.wasPressedThisFrame || externalPressed;
                    case 1:
                        return keyboard.sKey.wasPressedThisFrame || externalPressed;
                    case 2:
                        return keyboard.dKey.wasPressedThisFrame || externalPressed;
                    case 3:
                        return keyboard.fKey.wasPressedThisFrame || externalPressed;
                }
            }
#endif
            return externalPressed;
        }
    }
}
