using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnOnTheBass.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ControllerSpriteButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image targetImage;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite disabledSprite;
        [SerializeField] private bool selectOnPointerEnter = true;

        private ControllerMenuNavigator ownerMenu;
        private SongCarouselSelector ownerCarousel;
        private bool isActive;

        public Button Button => button != null ? button : GetComponent<Button>();
        public bool IsInteractable => Button != null && Button.IsInteractable();

        private void Reset()
        {
            button = GetComponent<Button>();
            targetImage = GetComponent<Image>();
            if (targetImage != null)
            {
                normalSprite = targetImage.sprite;
            }
        }

        private void Awake()
        {
            CacheReferences();
            ApplyState();
        }

        private void OnValidate()
        {
            CacheReferences();
            ApplyState();
        }

        public void RegisterOwner(ControllerMenuNavigator menu)
        {
            ownerMenu = menu;
        }

        public void RegisterOwner(SongCarouselSelector carousel)
        {
            ownerCarousel = carousel;
        }

        public void SetActiveState(bool active)
        {
            if (isActive == active)
            {
                return;
            }

            isActive = active;
            ApplyState();
        }

        public void Invoke()
        {
            Button selectableButton = Button;
            if (selectableButton != null && selectableButton.IsInteractable())
            {
                selectableButton.onClick.Invoke();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!selectOnPointerEnter)
            {
                return;
            }

            ownerMenu?.Select(this);
            ownerCarousel?.Select(this, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ownerMenu?.Select(this);
            ownerCarousel?.Select(this, false);
        }

        private void CacheReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }

            if (normalSprite == null && targetImage != null)
            {
                normalSprite = targetImage.sprite;
            }
        }

        private void ApplyState()
        {
            if (targetImage == null)
            {
                return;
            }

            Sprite nextSprite = normalSprite;
            if (!IsInteractable && disabledSprite != null)
            {
                nextSprite = disabledSprite;
            }
            else if (isActive && activeSprite != null)
            {
                nextSprite = activeSprite;
            }

            if (nextSprite != null)
            {
                targetImage.sprite = nextSprite;
            }
        }
    }
}
