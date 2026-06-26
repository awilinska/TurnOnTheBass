using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TurnOnTheBass.UI
{
    [DisallowMultipleComponent]
    public sealed class UiScreenSwitcher : MonoBehaviour
    {
        [SerializeField] private List<GameObject> screens = new List<GameObject>();
        [SerializeField] private GameObject startScreen;
        [SerializeField, Min(0f)] private float previousScreenDeactivateDelay = 2f;

        private Coroutine delayedDeactivateRoutine;
        private GameObject currentScreen;

        private void Start()
        {
            if (startScreen != null)
            {
                Show(startScreen);
            }
        }

        public void Show(GameObject screen)
        {
            if (screen == null)
            {
                return;
            }

            if (delayedDeactivateRoutine != null)
            {
                StopCoroutine(delayedDeactivateRoutine);
                delayedDeactivateRoutine = null;
            }

            GameObject previousScreen = currentScreen;
            currentScreen = screen;
            screen.SetActive(true);

            for (int index = 0; index < screens.Count; index++)
            {
                GameObject listedScreen = screens[index];
                if (listedScreen == null || listedScreen == screen || listedScreen == previousScreen)
                {
                    continue;
                }

                listedScreen.SetActive(false);
            }

            if (previousScreen != null && previousScreen != screen && previousScreen.activeSelf)
            {
                delayedDeactivateRoutine = StartCoroutine(DeactivateAfterDelay(previousScreen));
            }
        }

        public void ShowByIndex(int index)
        {
            if (index < 0 || index >= screens.Count)
            {
                return;
            }

            Show(screens[index]);
        }

        public void LoadScene(string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator DeactivateAfterDelay(GameObject screen)
        {
            if (previousScreenDeactivateDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(previousScreenDeactivateDelay);
            }

            if (screen != null && screen != currentScreen)
            {
                screen.SetActive(false);
            }

            delayedDeactivateRoutine = null;
        }
    }
}
