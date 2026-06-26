using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TurnOnTheBass.UI
{
    [DisallowMultipleComponent]
    public sealed class ControllerMenuNavigator : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private ControllerUiInputBindings input = new ControllerUiInputBindings();

        [Header("Buttons")]
        [SerializeField] private List<ControllerSpriteButton> buttons = new List<ControllerSpriteButton>();
        [SerializeField] private int startIndex;
        [SerializeField] private bool wrapNavigation = true;
        [SerializeField] private bool autoCollectChildButtons = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<int> selectionChanged;

        private int selectedIndex;

        public int SelectedIndex => selectedIndex;
        public ControllerSpriteButton SelectedButton => IsValidIndex(selectedIndex) ? buttons[selectedIndex] : null;

        private void Awake()
        {
            BuildButtonListIfNeeded();
            selectedIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, buttons.Count - 1));
            RegisterButtons();
            RefreshSelection(false);
        }

        private void OnEnable()
        {
            input.Enable();
            BuildButtonListIfNeeded();
            RegisterButtons();
            selectedIndex = GetNearestInteractableIndex(selectedIndex, 1);
            RefreshSelection(false);
        }

        private void OnDisable()
        {
            input.Disable();
        }

        private void Update()
        {
            if (buttons.Count == 0)
            {
                return;
            }

            if (input.TryConsumeVertical(out int direction))
            {
                Move(direction);
            }

            if (input.WasSubmitPressed())
            {
                SelectedButton?.Invoke();
            }
        }

        private void OnValidate()
        {
            startIndex = Mathf.Max(0, startIndex);
        }

        public void Select(ControllerSpriteButton button)
        {
            int index = buttons.IndexOf(button);
            if (index >= 0)
            {
                SelectIndex(index);
            }
        }

        public void SelectIndex(int index)
        {
            if (!IsValidIndex(index) || buttons[index] == null || !buttons[index].IsInteractable)
            {
                return;
            }

            if (selectedIndex == index)
            {
                RefreshSelection(false);
                return;
            }

            selectedIndex = index;
            RefreshSelection(true);
        }

        public void Move(int direction)
        {
            if (direction == 0 || buttons.Count == 0)
            {
                return;
            }

            int nextIndex = GetNearestInteractableIndex(selectedIndex + direction, direction);
            if (nextIndex >= 0)
            {
                SelectIndex(nextIndex);
            }
        }

        public void ActivateSelected()
        {
            SelectedButton?.Invoke();
        }

        private void BuildButtonListIfNeeded()
        {
            if (!autoCollectChildButtons || buttons.Count > 0)
            {
                buttons.RemoveAll(button => button == null);
                return;
            }

            GetComponentsInChildren(true, buttons);
        }

        private void RegisterButtons()
        {
            for (int index = 0; index < buttons.Count; index++)
            {
                if (buttons[index] != null)
                {
                    buttons[index].RegisterOwner(this);
                }
            }
        }

        private int GetNearestInteractableIndex(int desiredIndex, int direction)
        {
            if (buttons.Count == 0)
            {
                return -1;
            }

            int step = direction >= 0 ? 1 : -1;
            int index = desiredIndex;

            for (int attempts = 0; attempts < buttons.Count; attempts++)
            {
                if (wrapNavigation)
                {
                    index = WrapIndex(index);
                }
                else if (!IsValidIndex(index))
                {
                    return IsValidIndex(selectedIndex) ? selectedIndex : -1;
                }

                ControllerSpriteButton button = buttons[index];
                if (button != null && button.IsInteractable)
                {
                    return index;
                }

                index += step;
            }

            return IsValidIndex(selectedIndex) ? selectedIndex : -1;
        }

        private void RefreshSelection(bool notify)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, buttons.Count - 1));

            for (int index = 0; index < buttons.Count; index++)
            {
                if (buttons[index] != null)
                {
                    buttons[index].SetActiveState(index == selectedIndex);
                }
            }

            ControllerSpriteButton selectedButton = SelectedButton;
            if (selectedButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
            }

            if (notify)
            {
                selectionChanged.Invoke(selectedIndex);
            }
        }

        private int WrapIndex(int index)
        {
            if (buttons.Count == 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return buttons.Count - 1;
            }

            if (index >= buttons.Count)
            {
                return 0;
            }

            return index;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < buttons.Count;
        }
    }
}
