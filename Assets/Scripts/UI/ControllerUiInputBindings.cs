using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TurnOnTheBass.UI
{
    [Serializable]
    public sealed class ControllerUiInputBindings
    {
#if ENABLE_INPUT_SYSTEM
        [Header("Actions")]
        [Tooltip("Optional Vector2 navigation action. Use this for stick/dpad navigation.")]
        [SerializeField] private InputActionProperty navigateAction;
        [Tooltip("Optional button action for moving to the previous item.")]
        [SerializeField] private InputActionProperty upAction;
        [Tooltip("Optional button action for moving to the next item.")]
        [SerializeField] private InputActionProperty downAction;
        [Tooltip("Button action used to activate the currently selected item.")]
        [SerializeField] private InputActionProperty submitAction;
#endif

        [Header("Fallback")]
        [SerializeField] private bool useKeyboardFallback = true;

        [Header("Repeat")]
        [SerializeField, Range(0.1f, 1f)] private float axisThreshold = 0.55f;
        [SerializeField, Min(0.05f)] private float firstRepeatDelay = 0.35f;
        [SerializeField, Min(0.03f)] private float repeatInterval = 0.12f;

        private int heldDirection;
        private float nextRepeatTime;

        public void Enable()
        {
#if ENABLE_INPUT_SYSTEM
            EnableAction(navigateAction.action);
            EnableAction(upAction.action);
            EnableAction(downAction.action);
            EnableAction(submitAction.action);
#endif
            ResetRepeat();
        }

        public void Disable()
        {
            ResetRepeat();
        }

        public bool TryConsumeVertical(out int direction)
        {
            direction = 0;

#if ENABLE_INPUT_SYSTEM
            if (WasPressed(upAction.action))
            {
                direction = -1;
                ResetRepeat();
                return true;
            }

            if (WasPressed(downAction.action))
            {
                direction = 1;
                ResetRepeat();
                return true;
            }

            InputAction navigation = navigateAction.action;
            if (navigation != null)
            {
                Vector2 input = navigation.ReadValue<Vector2>();
                if (TryConsumeAxis(input.y, out direction))
                {
                    return true;
                }
            }

            if (useKeyboardFallback)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                    {
                        direction = -1;
                        ResetRepeat();
                        return true;
                    }

                    if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                    {
                        direction = 1;
                        ResetRepeat();
                        return true;
                    }
                }
            }
#endif

            return false;
        }

        public bool WasSubmitPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (WasPressed(submitAction.action))
            {
                return true;
            }

            if (useKeyboardFallback)
            {
                Keyboard keyboard = Keyboard.current;
                return keyboard != null &&
                       (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            }
#endif
            return false;
        }

        private bool TryConsumeAxis(float axisValue, out int direction)
        {
            direction = 0;
            int requestedDirection = 0;

            if (axisValue >= axisThreshold)
            {
                requestedDirection = -1;
            }
            else if (axisValue <= -axisThreshold)
            {
                requestedDirection = 1;
            }

            if (requestedDirection == 0)
            {
                ResetRepeat();
                return false;
            }

            float now = Time.unscaledTime;
            if (requestedDirection != heldDirection)
            {
                heldDirection = requestedDirection;
                nextRepeatTime = now + firstRepeatDelay;
                direction = requestedDirection;
                return true;
            }

            if (now < nextRepeatTime)
            {
                return false;
            }

            nextRepeatTime = now + repeatInterval;
            direction = requestedDirection;
            return true;
        }

        private void ResetRepeat()
        {
            heldDirection = 0;
            nextRepeatTime = 0f;
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnableAction(InputAction action)
        {
            if (action != null && !action.enabled)
            {
                action.Enable();
            }
        }

        private static bool WasPressed(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }
#endif
    }
}
