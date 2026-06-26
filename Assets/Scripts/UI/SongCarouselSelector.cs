using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TurnOnTheBass.UI
{
    [Serializable]
    public sealed class SongCarouselItem
    {
        [SerializeField] private string songId;
        [SerializeField] private string displayName;
        [SerializeField] private RhythmSongDefinition songDefinition;
        [SerializeField] private ControllerSpriteButton button;
        [SerializeField] private UnityEvent selected;

        public string SongId => songId;
        public string DisplayName => displayName;
        public RhythmSongDefinition SongDefinition => songDefinition;
        public ControllerSpriteButton Button => button;

        public void Invoke()
        {
            selected.Invoke();
        }
    }

    [Serializable]
    public sealed class SongCarouselItemEvent : UnityEvent<SongCarouselItem>
    {
    }

    [DisallowMultipleComponent]
    public sealed class SongCarouselSelector : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private ControllerUiInputBindings input = new ControllerUiInputBindings();

        [Header("Items")]
        [SerializeField] private List<SongCarouselItem> songs = new List<SongCarouselItem>();
        [SerializeField] private int startIndex;
        [SerializeField] private bool wrapNavigation = true;

        [Header("Layout")]
        [SerializeField] private RectTransform centerPoint;
        [SerializeField, Min(1f)] private float verticalSpacing = 140f;
        [SerializeField, Min(0f)] private float sideOffset = 0f;
        [SerializeField, Range(0.1f, 1f)] private float inactiveScale = 0.75f;
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.45f;
        [SerializeField, Min(0.01f)] private float moveSharpness = 14f;
        [SerializeField] private bool animate = true;

        [Header("Events")]
        [SerializeField] private SongCarouselItemEvent selectionChanged;
        [SerializeField] private SongCarouselItemEvent songConfirmed;

        [Header("Rhythm Game")]
        [SerializeField] private CanvasRhythmGame rhythmGame;
        [SerializeField] private UiScreenSwitcher screenSwitcher;
        [SerializeField] private GameObject rhythmGameScreen;
        [SerializeField] private bool showRhythmScreenOnConfirm = true;
        [SerializeField] private bool beginRhythmGameOnConfirm = true;

        private int selectedIndex;

        public int SelectedIndex => selectedIndex;
        public SongCarouselItem SelectedSong => IsValidIndex(selectedIndex) ? songs[selectedIndex] : null;

        private void Awake()
        {
            songs.RemoveAll(song => song == null || song.Button == null);
            selectedIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, songs.Count - 1));
            RegisterButtons();
            RefreshState(false, true);
        }

        private void OnEnable()
        {
            input.Enable();
            RegisterButtons();
            RefreshState(false, true);
        }

        private void OnDisable()
        {
            input.Disable();
        }

        private void Update()
        {
            if (songs.Count == 0)
            {
                return;
            }

            if (input.TryConsumeVertical(out int direction))
            {
                Move(direction);
            }

            if (input.WasSubmitPressed())
            {
                ConfirmSelection();
            }

            if (animate)
            {
                ApplyLayout(false);
            }
        }

        private void OnValidate()
        {
            startIndex = Mathf.Max(0, startIndex);
            verticalSpacing = Mathf.Max(1f, verticalSpacing);
            moveSharpness = Mathf.Max(0.01f, moveSharpness);
        }

        public void Select(ControllerSpriteButton button, bool notify)
        {
            for (int index = 0; index < songs.Count; index++)
            {
                if (songs[index] != null && songs[index].Button == button)
                {
                    SelectIndex(index, notify);
                    return;
                }
            }
        }

        public void SelectIndex(int index)
        {
            SelectIndex(index, true);
        }

        public void Move(int direction)
        {
            if (direction == 0 || songs.Count == 0)
            {
                return;
            }

            int nextIndex = selectedIndex + direction;
            if (wrapNavigation)
            {
                nextIndex = WrapIndex(nextIndex);
            }
            else
            {
                nextIndex = Mathf.Clamp(nextIndex, 0, songs.Count - 1);
            }

            SelectIndex(nextIndex, true);
        }

        public void ConfirmSelection()
        {
            SongCarouselItem song = SelectedSong;
            if (song == null)
            {
                return;
            }

            song.Invoke();
            songConfirmed.Invoke(song);

            if (showRhythmScreenOnConfirm && screenSwitcher != null && rhythmGameScreen != null)
            {
                screenSwitcher.Show(rhythmGameScreen);
            }

            if (beginRhythmGameOnConfirm && rhythmGame != null)
            {
                rhythmGame.BeginSong(song.SongDefinition);
            }
        }

        public void ConfirmSelectionForButton(ControllerSpriteButton button)
        {
            Select(button, false);
            ConfirmSelection();
        }

        private void SelectIndex(int index, bool notify)
        {
            if (!IsValidIndex(index) || selectedIndex == index)
            {
                RefreshState(false, false);
                return;
            }

            selectedIndex = index;
            RefreshState(notify, false);
        }

        private void RegisterButtons()
        {
            for (int index = 0; index < songs.Count; index++)
            {
                ControllerSpriteButton button = songs[index]?.Button;
                if (button != null)
                {
                    button.RegisterOwner(this);
                }
            }
        }

        private void RefreshState(bool notify, bool snap)
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, songs.Count - 1));

            for (int index = 0; index < songs.Count; index++)
            {
                ControllerSpriteButton button = songs[index]?.Button;
                if (button != null)
                {
                    button.SetActiveState(index == selectedIndex);
                }
            }

            ControllerSpriteButton selectedButton = SelectedSong?.Button;
            if (selectedButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
            }

            ApplyLayout(snap || !animate);

            if (notify)
            {
                selectionChanged.Invoke(SelectedSong);
            }
        }

        private void ApplyLayout(bool snap)
        {
            Vector2 center = GetCenterPosition();
            float t = snap ? 1f : 1f - Mathf.Exp(-moveSharpness * Time.unscaledDeltaTime);

            for (int index = 0; index < songs.Count; index++)
            {
                ControllerSpriteButton button = songs[index]?.Button;
                if (button == null)
                {
                    continue;
                }

                RectTransform rect = button.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                int offset = GetVisualOffset(index);
                bool active = index == selectedIndex;
                Vector2 targetPosition = center + new Vector2(Mathf.Abs(offset) * sideOffset, offset * verticalSpacing);
                Vector3 targetScale = Vector3.one * (active ? 1f : inactiveScale);
                float targetAlpha = active ? 1f : inactiveAlpha;

                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPosition, t);
                rect.localScale = Vector3.Lerp(rect.localScale, targetScale, t);
                SetCanvasGroupAlpha(button.gameObject, targetAlpha, t, snap);
                rect.SetSiblingIndex(GetSiblingIndexForOffset(offset));
            }
        }

        private Vector2 GetCenterPosition()
        {
            if (centerPoint != null)
            {
                return centerPoint.anchoredPosition;
            }

            return Vector2.zero;
        }

        private int GetVisualOffset(int index)
        {
            int offset = index - selectedIndex;
            if (!wrapNavigation || songs.Count <= 1)
            {
                return offset;
            }

            int half = songs.Count / 2;
            if (offset > half)
            {
                offset -= songs.Count;
            }
            else if (offset < -half)
            {
                offset += songs.Count;
            }

            return offset;
        }

        private int GetSiblingIndexForOffset(int offset)
        {
            return Mathf.Max(0, songs.Count - Mathf.Abs(offset));
        }

        private static void SetCanvasGroupAlpha(GameObject target, float alpha, float t, bool snap)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.AddComponent<CanvasGroup>();
            }

            group.alpha = snap ? alpha : Mathf.Lerp(group.alpha, alpha, t);
        }

        private int WrapIndex(int index)
        {
            if (songs.Count == 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return songs.Count - 1;
            }

            if (index >= songs.Count)
            {
                return 0;
            }

            return index;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < songs.Count;
        }
    }
}
